# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build

# Run all tests
dotnet test

# Run a single test class
dotnet test tests/NSchema.Core.Tests --filter "FullyQualifiedName~DefaultSchemaComparerTests"
```

Note: the Postgres provider was extracted to its own repository (see commit `dd3e695`). The solution (`NSchema.Core.slnx`) contains only `src/NSchema.Core` and `tests/NSchema.Core.Tests`, where all real code lives.

## Architecture

NSchema is a declarative database schema migration library for .NET. The user describes the schema they want via `AbstractSchemaProvider`; NSchema introspects the database, diffs, and applies the difference.

`NSchemaApplication.CreateBuilder()` returns an `NSchemaApplicationBuilder`, which uses a `HostApplicationBuilder` internally purely to compose configuration, logging, metrics, and the DI container. `Build()` produces an `NSchemaApplication` (a plain `IDisposable`, not an `IHost`). It does **not** run as a host: calling `Plan()`/`PlanDestroy()`/`Apply()`/`Refresh()`/`Import()`/`Validate()`/`Destroy()`/`Show()`/`Drift()`/`ForceUnlock()` resolves that operation's dedicated interface (`IPlanOperation`, `IApplyOperation`, …) from DI and `await`s its `Execute(arguments, ct)` directly, passing the matching arguments record. Exceptions propagate to the caller. There is no `BackgroundService` and no host lifecycle. The app is single-run — a second invocation throws.

### Layer separation

The codebase is split into a **domain layer** and an **application layer**:

- **Domain layer.** Pure planning logic, organized as one namespace per pipeline stage:
  - `Schema/` — the schema model, desired/current providers, `ISchemaTransformer`, `ISchemaPolicy`.
  - `Diff/` — the structured diff model (`Diff/Model/`, rooted at `DatabaseDiff`), `ISchemaComparer`, `IDiffTransformer`, `IDiffPolicy`, and the diff renderer.
  - `Plan/` — the executable plan model (`Plan/Model/`, rooted at `MigrationPlan`), `IPlanLinearizer`, `IPlanTransformer`, `IPlanPolicy`, and the planner that runs all three stages: `IMigrationPlanner` (default `DefaultMigrationPlanner`) and its `MigrationPlanResult`. The planner has no knowledge of operations or how a run is orchestrated.
- **Application layer.** Orchestration of a run:
  - `Operations/` — one vertical slice per operation (`Operations/Plan/`, `Operations/PlanDestroy/`, `Operations/Apply/`, `Operations/Refresh/`, `Operations/Import/`, `Operations/Validate/`, `Operations/Destroy/`, `Operations/Show/`, `Operations/Drift/`, `Operations/ForceUnlock/`), each holding a public interface (`IPlanOperation`, …), a public arguments record (`PlanArguments`, …, empty where the operation has no inputs yet), and the internal handler. Also `IMigrationWorkflow` (the imperative shell operations share — schema resolution, planning, and state capture, under `Operations/Services/`), the default `IOperationReporter` (`DefaultOperationReporter`), the default `IOperationConfirmation` (`AutoApproveConfirmation`), and `IOperationConfirmation` itself (`Operations/Confirmation/`).

### Operations

Each operation is its own seam: an **internal** `I{Name}Operation` interface with `Execute({Name}Arguments arguments, CancellationToken)`, an internal handler, and a **public** arguments record (the discoverable home for that operation's inputs). `NSchemaApplication` resolves the interface with `GetRequiredService<I{Name}Operation>()` and passes the arguments. The interfaces are internal because operations are invoked via the public methods on `NSchemaApplication` (`Plan()`, `Apply()`, …), not by user code resolving the handler; only the arguments records are public. There is no shared `IOperation` marker and no `OperationKind` enum — adding an operation means a new slice folder (interface + arguments + handler), a `TryAddSingleton` registration, and a public method (plus arguments overload) on `NSchemaApplication`.

Built-in operations (`src/NSchema.Core/Operations/`):

- **`PlanOperation`** — plans against the offline source (preferred), reports the plan and the SQL preview (if an `ISqlGenerator` dialect is registered). Does not execute.
- **`PlanDestroyOperation`** — previews the teardown plan: calls `IMigrationWorkflow.PlanDestroy` (the same trusted `PlanTeardown` path `Destroy` uses, bypassing the diff/plan transformers and policies) and reports the SQL preview (if a generator is registered). The preview-half of `Destroy`, mirroring how `Plan` previews `Apply`. Does not confirm, execute, or capture state. The analogue of `terraform plan -destroy`.
- **`ApplyOperation`** — plans against the online source (required), generates SQL via `ISqlGenerator`, previews it, executes via `ISqlExecutor`, and captures state.
- **`RefreshOperation`** — captures the live schema to the state store without planning or applying.
- **`ImportOperation`** — fetches the live schema (optionally filtered by `ImportArguments.Schemas` / `ImportArguments.Tables`), then writes it to the local filesystem as desired-schema source files. The destination is per-invocation: `ImportArguments.OutputFile` (when `Partition` is `None`) or `ImportArguments.OutputDirectory` (for the partitioned modes), serialized via `IKeyedResolver<ISchemaSerializer>` keyed by `ImportArguments.Format`. Import is additive: existing tables in the target files are preserved. The file-writing logic (partitioning + additive merge) lives directly in the handler — there is no `ISchemaImportTarget` seam.
- **`ValidateOperation`** — loads the desired schema and validates it against the registered `ISchemaPolicy` implementations, without planning or applying.
- **`DestroyOperation`** — tears down the managed schema. It reads the managed schema from the state store (offline) when one is configured, otherwise from the declared desired schema, and diffs it against an empty schema to produce drops, then generates SQL and executes it (online required), like `ApplyOperation`. Destroy uses `IMigrationPlanner.PlanTeardown`, a **trusted path that bypasses the diff/plan transformers and policies** (so a custom policy can't block teardown and a transformer can't silently alter it); the destructive-action policy therefore never runs. `IOperationConfirmation` still gates execution.
- **`ShowOperation`** — reads the recorded (offline) state from the state store and reports it via `IOperationReporter.ReportSchema`, without planning or contacting the live database. The analogue of `terraform show`. Requires a state store (offline read throws otherwise).
- **`DriftOperation`** — reads the recorded (offline) state and the live (online) schema, compares them with `ISchemaComparer` (direction recorded → live, so an out-of-band add reads as `Add`, an out-of-band drop as `Remove`), and reports the resulting `DatabaseDiff`. The analogue of `terraform plan -refresh-only`. Requires both a state store and a live provider. The comparison is a pure observation — no diff/plan transformers or policies run — so it never fails on a policy violation.
- **`ForceUnlockOperation`** — gates on `IOperationConfirmation` (a `ForceUnlockConfirmationRequest`, which is `IsDestructive`), then calls `IStateLock.ForceUnlock` to remove a stale state lock regardless of holder, and reports who held it. The analogue of `terraform force-unlock` (which also prompts before overriding the lock). Does not plan, apply, or touch the schema. No-op (reports "no lock held") under the default `NoOpStateLock`.

The shared orchestration used by `PlanOperation`, `PlanDestroyOperation`, `ApplyOperation`, `DestroyOperation`, `RefreshOperation`, and `ValidateOperation` lives in **`IMigrationWorkflow`** (`src/NSchema.Core/Operations/Services/`) — the imperative shell around the pure `IMigrationPlanner`: it collects desired providers, derives scope, fetches the current schema, calls the planner, reports the diff/plan, and captures state to the store. State capture goes through `Refresh(RefreshMode)`: `Required` (the `Refresh` operation) throws when no store is configured, `Optional` (post-apply / post-destroy) is a silent no-op when there's no store. `ShowOperation` and `DriftOperation` do **not** use the workflow — they are thin read-only inspections that talk to `ICurrentSchemaProvider` (and, for drift, `ISchemaComparer`) directly, since there is no desired schema to resolve, no plan to linearize, and no state to capture.

### Confirmation

`IOperationConfirmation` (`src/NSchema.Core/Operations/Confirmation/`) gates `Apply`, `Destroy`, and `ForceUnlock` before each makes a change worth reviewing. Its `Confirm` method receives an `OperationConfirmationRequest` — an abstract record carrying only an `IsDestructive` flag — with sealed `ApplyConfirmationRequest` / `DestroyConfirmationRequest` (each carrying the `MigrationPlan`) and `ForceUnlockConfirmationRequest` (no plan — there is none) subtypes. The plan lives on the plan-bearing subtypes rather than the base, since force-unlock has no plan to show. A front-end can switch on the concrete type for a tailored prompt, or read `IsDestructive` to gate teardowns and lock overrides more strongly. The default `AutoApproveConfirmation` (in `Operations/`) approves everything without prompting.

### Schema providers

`ISchemaProvider` (`src/NSchema.Core/Schema/ISchemaProvider.cs`) is used for **desired-state** providers only:

```csharp
ValueTask<DatabaseSchema> GetSchema(string[]? schemaNames = null, CancellationToken cancellationToken = default);
```

Desired providers are registered as an enumerable `ISchemaProvider` via `AddSchema<T>()` or assembly scanning, and aggregated into a single schema by `IDesiredSchemaProvider`.

**Current-state** schema access goes through `ICurrentSchemaProvider` (`src/NSchema.Core/Schema/ICurrentSchemaProvider.cs`):

```csharp
ValueTask<DatabaseSchema> GetSchema(SchemaSourceMode preferred, string[]? schemaNames, bool required = true, CancellationToken cancellationToken = default);
```

- `GetSchema(Online, …)` — reads from the live database provider (registered via `UseCurrentSchema<T>()`). Throws if not configured.
- `GetSchema(Offline, …)` — reads from the state-backed source (requires a registered `ISchemaStateStore`). Throws if not configured.
- `GetSchema(Offline, …, required: false)` — prefers offline, falls back to online, throws only if neither exists.

The internal `DefaultCurrentSchemaProvider` wires both sources together. Registering a state store via `UseStateStore*()` is all that's needed to enable offline planning.

`ISchemaStateStore` deals only in **serialized payloads** (`Task<ReadOnlyMemory<byte>?> Read(...)` / `Task Write(ReadOnlyMemory<byte>, ...)`) — it's a persistence sink (file, blob, …) and never sees the schema model. The core owns the format: it serializes via the internal `ISchemaStateSerializer` before writing (in `IMigrationWorkflow.Refresh`) and deserializes after reading (in `StateBackedSchemaProvider`). A custom store therefore only implements load/save of an opaque byte payload.

### State locking

`IStateLock` (`src/NSchema.Core/State/IStateLock.cs`) coordinates exclusive access to the shared state so two state-mutating operations can't run against it concurrently. It is a **separate seam** from `ISchemaStateStore` (single responsibility, independently optional), but a single backend may implement **both** — e.g. a future S3 backend that persists state and holds the lock in the same bucket. `Acquire(StateLockRequest, ct)` returns an `IStateLockHandle` (an `IAsyncDisposable`); disposing it releases the lock. When the lock is already held, `Acquire` throws `StateLockedException` (carrying the holder's `StateLockInfo` when the implementation can read it). `ForceUnlock(ct)` removes the current lock regardless of holder (returning the removed `StateLockInfo`, or `null` if nothing was held) — for recovering a stale lock; it backs the `ForceUnlock` operation (`app.ForceUnlock()`).

The three **state-mutating** operations acquire the lock for the whole run via `await using`: `ApplyOperation` (`"apply"`), `DestroyOperation` (`"destroy"`), and `RefreshOperation` (`"refresh"`). Read-only operations (`Plan`, `PlanDestroy`, `Show`, `Drift`, `Validate`) never lock.

**The lock is registered automatically alongside the store**, since using a separate state and lock backend would be unusual. `UseFileStateStore(path)` also registers a matching `FileStateLock` at `<path>.lock`; `UseStateStore<T>()` / `UseStateStore(instance)` register the same instance as the lock when the backend also implements `IStateLock` (and otherwise leave locking off). The store registration never overrides an **explicit** lock choice (`UseStateLock<T>()` / `UseStateLock(instance)` / `UseFileStateLock(path)`), regardless of call order — the builder tracks an explicit choice via a private `_stateLockConfigured` flag. With no store and no explicit lock, the default `NoOpStateLock` makes acquisition a no-op, so locking is off. (`FileStateLock` is a local-dev lock-file, not a distributed lock.)

### Planner

`DefaultMigrationPlanner` (`src/NSchema.Core/Plan/DefaultMigrationPlanner.cs`) is a pure domain service. It takes two pre-resolved `DatabaseSchema` values and produces a `MigrationPlanResult` (which carries the executable `MigrationPlan`, its structured `DatabaseDiff`, and any `PolicyDiagnostic`s). The **structured diff is the primary artifact**: the comparer emits it directly, and the linearizer derives the ordered plan from it. The pipeline is three stages, each of which transforms its representation and then validates it:

1. **Schema stage** — `ISchemaTransformer`s are applied upstream by `IDesiredSchemaProvider` when it produces the desired schema; the planner then runs every `ISchemaPolicy` against it. A schema-policy error is fatal and skips the rest. This stage is also exposed on its own as `IMigrationPlanner.Validate(desired)`, which the validate operation uses.
2. **Diff stage** — `ISchemaComparer` produces the structured `DatabaseDiff` directly (no flat-action intermediate). `IDiffTransformer`s then run in registration order, and every `IDiffPolicy` validates the result. The built-in `DestructiveActionDiffPolicy` runs here (it reasons over `ChangeKind.Remove` and narrowing column changes) and enforces its own `DestructiveActionOptions.Policy`.
3. **Plan stage** — `IPlanLinearizer` (default `DefaultPlanLinearizer`) walks the diff and emits the migration actions in a safe dependency order. The planner then attaches the collected deployment scripts to the `MigrationPlan` as its `PreDeploymentScripts` / `PostDeploymentScripts` (scripts aren't a diff concept, so they live on the plan rather than in `Actions`). `IPlanTransformer`s then run in registration order, and every `IPlanPolicy` validates the final plan. At execution time the script SQL is composed around the generated statements (pre first, post last) — scripts are raw SQL and need no dialect translation.

The planner has no knowledge of operations, online/offline routing, or the application/operation options.

### SQL generation and execution

Generating SQL and executing it are deliberately separate, so a plan can be previewed offline:

- **`ISqlGenerator`** (`AddSqlGenerator<T>(dialect)`, typically supplied by a database-provider extension) turns a `MigrationPlan` into a structured `SqlPlan` (an ordered list of `SqlStatement`s, each flagged if it `RunOutsideTransaction`). This is pure string-building — no connection — so the SQL preview works offline whenever a dialect is registered. Each generator declares a `Dialect`; operations resolve the one for the run via `IKeyedResolver<ISqlGenerator>`, keyed by `SqlOptions.Dialect`. The generator is optional: with none registered or no `WithDialect(...)` set, `HasCurrent` is `false` and `PlanOperation` reports the plan without a SQL preview.
- **`ISqlExecutor`** (default `DefaultSqlExecutor`) executes the `SqlPlan` against a `DbDataSource`. It reads `SqlOptions.TransactionMode` to decide whether to wrap everything in one transaction. It is the only online step.

The operations consume these two seams directly — there is no separate compiler abstraction. The `SqlPlan` reaches the reporter as a structured value via `IOperationReporter.ReportSqlPlan(SqlPlan)`, which renders it through **`ISqlPlanRenderer`** (default `DefaultSqlPlanRenderer`), mirroring the diff side's `IDiffRenderer`.

### Defining a schema

Subclass `AbstractSchemaProvider` and call `Schema()` / `Table()` / `Column()` etc. via the fluent builders. The fluent API supports both a return style and a delegate style:

```csharp
public class MySchema : AbstractSchemaProvider
{
    public MySchema()
    {
        var users = Schema("app").Table("users");
        users.Column("id", SqlType.Int).PrimaryKey("users_pkey");
        users.Column("name", SqlType.Text).NotNull();
    }
}
```

Providers are registered with `builder.AddSchema<T>()` or `builder.AddSchemasFromAssemblyContaining<T>()`.

### Extension points

| Interface                               | Registered via                                                                                                                  |
|-----------------------------------------|---------------------------------------------------------------------------------------------------------------------------------|
| `ISchemaProvider` (desired)             | `AddSchema<T>()` / `AddSchemasFromAssembly[Containing]<T>()`                                                                    |
| `ISchemaProvider` (online current)      | `UseCurrentSchema<T>()` — typically called from a database-provider extension (e.g. `UsePostgres(...)`)                         |
| `I{Name}Operation` (one per operation)  | `TryAddSingleton<I{Name}Operation, {Name}Operation>()` (built-in; replace before `Build()` to override)                         |
| `IOperationConfirmation`                | `Services.AddSingleton<IOperationConfirmation, T>()` (default `AutoApproveConfirmation`, registered with `TryAdd`)              |
| `ISchemaTransformer`                    | `AddSchemaTransformer<T>()` / `AddSchemaTransformersFromAssembly[Containing]<T>()`                                              |
| `ISchemaPolicy`                         | `AddSchemaPolicy<T>()`                                                                                                          |
| `IDiffTransformer`                      | `AddDiffTransformer<T>()` / `AddDiffTransformersFromAssembly[Containing]<T>()`                                                  |
| `IDiffPolicy`                           | `AddDiffPolicy<T>()` / `AddDiffPoliciesFromAssembly[Containing]<T>()`                                                           |
| `IPlanTransformer`                      | `AddPlanTransformer<T>()` / `AddPlanTransformersFromAssembly[Containing]<T>()`                                                  |
| `IPlanPolicy`                           | `AddPlanPolicy<T>()` / `AddPlanPoliciesFromAssembly[Containing]<T>()`                                                           |
| `IScriptProvider`                       | `AddScripts(provider)` / `AddScriptFromFile(...)` / `AddScriptsFromEmbeddedResources(...)`                                      |
| `ISqlExecutor`                          | `UseSqlExecutor<T>()` (replaces default)                                                                                        |
| `ISchemaStateStore`                     | `UseStateStore<T>()` / `UseStateStore(instance)` / `UseFileStateStore(path)`                                                    |
| `IStateLock`                            | auto co-located with the store; override with `UseStateLock<T>()` / `UseStateLock(instance)` / `UseFileStateLock(path)`         |
| `IOperationReporter` (keyed by name)    | `AddReporter<T>(format)` / `AddReporter(format, instance)` (last-wins per key); select via `NSchemaApplicationOptions.Reporter` |
| `ISqlGenerator` (keyed by `Dialect`)    | `AddSqlGenerator<T>(dialect)`, typically a database-provider extension; select with `WithDialect(...)`                          |
| `ISchemaSerializer` (keyed by `Format`) | `AddSchemaSerializer<T>(format)` (first-wins); `UseSchemaSerializer<T>(format)` to replace (JSON built-in)                      |
| `IDiffRenderer`                         | `UseTerraformRenderer(...)` / `UseDiffRenderer<TRenderer>()`                                                                    |
| `ISchemaRenderer`                       | `UseSchemaRenderer<TRenderer>()` (default `DefaultSchemaRenderer`)                                                              |
| `ISqlPlanRenderer`                      | `UseSqlPlanRenderer<TRenderer>()` (default `DefaultSqlPlanRenderer`)                                                            |

The planning algorithm and aggregators — `ISchemaComparer`, `IPlanLinearizer`, `IMigrationPlanner`, `ICurrentSchemaProvider`, `IDesiredSchemaProvider` — and the state serializer (`ISchemaStateSerializer`) are **internal**. They remain interfaces for DI wiring and test mocking, but they are not extension points and not replaceable from user code. (`IKeyedResolver<TValue>` is public — it's the resolver consumers inject to pick a keyed implementation — but the per-operation handler interfaces are internal.)

### Resolving one of many (resolver pattern)

Several public seams let you register multiple implementations and select one by key. Selection goes through the shared `IKeyedResolver<TValue>` interface (`Resolution/`), backed by DI keyed services; it's injected into consumers (including front-ends — e.g. the CLI's init command injects `IKeyedResolver<ISchemaSerializer>` to write a demo schema). `IOperationReporter` (by reporter name), `ISqlGenerator` (by `Dialect`), and `ISchemaSerializer` (by `Format`) read the key for the current run from options (via `Current`). The import operation also resolves `ISchemaSerializer` **explicitly** from `ImportArguments.Format` (via `Resolve(key)`). `IOperationReporter` uses last-wins registration (`Services.Replace`); `ISqlGenerator` and `ISchemaSerializer` use first-wins (`TryAddKeyedSingleton`), and `ISchemaSerializer` has a `UseSchemaSerializer<T>(format)` method to replace the built-in.

`IKeyedResolver<TValue>` is injected directly into consumers and exposes:
- `Current` — resolves the implementation for the current run's configured key (e.g. `NSchemaApplicationOptions.Reporter`, `SqlOptions.Dialect`). Throws if no key is configured or the key isn't registered.
- `HasCurrent` — returns `true` if `Current` would succeed; use this to guard optional seams (e.g. SQL generators).
- `Resolve(key)` / `TryResolve(key, out value)` — resolve by explicit key (how the import operation selects its target).

The `DefaultKeyedResolver<TValue, TOptions>` implementation reads the current key from `IOptions<TOptions>` via a selector delegate supplied at registration time; a resolver registered without a selector (e.g. `ISchemaSerializer`) has no `Current` and is used only via `Resolve` (how the import operation selects its serializer by `ImportArguments.Format`).

### Diff rendering

The structured `DatabaseDiff` (`Diff/Model/`: schema → table → columns/indexes/constraints/grants, each carrying a `ChangeKind` of `Add`/`Modify`/`Remove`) is produced directly by `ISchemaComparer` during planning and carried on `MigrationPlanResult.Diff`. The model is presentation-agnostic, and it is the semantic source of truth — `IPlanLinearizer` derives the executable plan from it, so a diff transformer that changes the tree also changes what executes.

Rendering is a single phase: **`IDiffRenderer`** (default `TerraformDiffRenderer`) turns a `DatabaseDiff` into text. The default emits a Terraform-style diff; an alternative (e.g. JSON) can be registered without touching the diff projection. Each renderer owns its own options POCO — the Terraform renderer reads `TerraformDiffRendererOptions.IncludeColour`, which defaults via its property initializer to `EnvironmentHelpers.SupportsColor` (on unless `NO_COLOR` is set or output is redirected). Override it via `UseTerraformRenderer(o => ...)`. The renderer itself never reads the environment.

`IOperationReporter.ReportDiff(DatabaseDiff)` is the seam: the reporter owns the `IDiffRenderer` and writes the rendered text to its output. `IMigrationWorkflow` hands `result.Diff` to the reporter (alongside `ReportPlan(result.Plan)`) — there is no separate diff-building step.

There is a parallel seam for rendering a **single schema state** (rather than a change between two): **`ISchemaRenderer`** (default `DefaultSchemaRenderer`, an indented schema → table → columns/pk/fks/indexes/grants tree) turns a `DatabaseSchema` into text, and `IOperationReporter.ReportSchema(DatabaseSchema)` is its seam — the reporter owns the `ISchemaRenderer` and writes the rendered text out, mirroring the diff side. The `Show` operation reports through it. Replace the renderer via `UseSchemaRenderer<TRenderer>()`.

### Renaming

Schemas, tables, and columns support rename detection via the fluent `RenamedFrom(oldName)` method, which sets the `OldName` property on the domain model. The comparer matches on `OldName` when the current name isn't found.

### Configuration options

**`DestructiveActionOptions`** (`NSchema.Diff.Policies`) — owned by the diff policy it configures:
- `Policy` — a `DestructiveActionPolicy`: `Error` (default), `Warn`, or `Allow`. Enforced by `DestructiveActionDiffPolicy` (which reads `IOptions<DestructiveActionOptions>`). Configured via `WithDestructiveActionPolicy(...)`.

Schema scope is **not** an option — it's a per-invocation argument. `PlanArguments` / `ApplyArguments` / `ValidateArguments` carry a `Schemas` filter; when `null`, scope is derived from the desired schema. `IMigrationWorkflow` takes the scope as an explicit parameter rather than reading ambient options. (`Destroy` is unscoped — it always tears down the whole managed schema.)

**`NSchemaApplicationOptions`** (`NSchema`) — how the application is constructed and run. Passed to `CreateBuilder(options)` and registered as a singleton; its values are fixed at build time (`init`-only):
- `Args` / `ApplicationName` / `EnvironmentName` / `ContentRootPath` — consumed by the builder constructor to configure the underlying `HostApplicationBuilder`.
- `ExceptionBehavior` — `ReportAndThrow` (default) or `Throw`. Read by `NSchemaApplication` when an operation throws: `ReportAndThrow` reports via the resolved `IOperationReporter` before rethrowing, `Throw` just rethrows.
- `Reporter` — the `IOperationReporter` key to render with (defaults to `DefaultOperationReporter.ReporterName`, `"default"`); resolved through `IKeyedResolver<IOperationReporter>.Current`.

The operation to run is **not** an option — it's chosen by which method you call on the built `NSchemaApplication` (`Plan()` / `PlanDestroy()` / `Apply()` / `Refresh()` / `Import()` / `Validate()` / `Destroy()` / `Show()` / `Drift()` / `ForceUnlock()`). Each method takes that operation's arguments record (e.g. `Import(ImportArguments)`).

**`SqlOptions`** (`NSchema.Sql`) — SQL generation and execution:
- `Dialect` — the `ISqlGenerator` dialect to generate (must be set explicitly when a generator is registered). Configured via `WithDialect(...)`; resolved through `IKeyedResolver<ISqlGenerator>.Current`.
- `TransactionMode` — `Single` (default; whole plan in one transaction, with carve-outs for statements marked `RunOutsideTransaction`) or `None`. Configured via `WithTransactionMode(...)`.

**`ImportArguments`** (`NSchema.Operations.Import`) — per-invocation import inputs, passed to `Import(...)`:
- `Schemas` — optional `string[]` scope filter; only these schemas are fetched from the live database.
- `Tables` — optional `string[]` table filter applied after fetching.
- `OutputFile` — the file to write to when `Partition` is `None` (defaults to `schema.json`).
- `OutputDirectory` — the root directory to write into when `Partition` is `Schema` or `Table` (defaults to `.`).
- `Partition` — an `ImportPartitionMode` (`None` / `Schema` / `Table`) controlling how the import is split across files.
- `Format` — the `ISchemaSerializer` format key to write with, resolved via `IKeyedResolver<ISchemaSerializer>.Resolve(...)` (defaults to `json`).
