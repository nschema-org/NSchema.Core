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

`NSchemaApplication.CreateBuilder()` returns an `NSchemaApplicationBuilder`, which uses a `HostApplicationBuilder` internally purely to compose configuration, logging, metrics, and the DI container. `Build()` produces an `NSchemaApplication` (a plain `IDisposable`, not an `IHost`). It does **not** run as a host: calling `Plan()`/`Apply()`/`Refresh()`/`Import()`/`Validate()`/`Destroy()` resolves that operation's dedicated interface (`IPlanOperation`, `IApplyOperation`, …) from DI and `await`s its `Execute(arguments, ct)` directly, passing the matching arguments record. Exceptions propagate to the caller. There is no `BackgroundService` and no host lifecycle. The app is single-run — a second invocation throws.

### Layer separation

The codebase is split into a **domain layer** and an **application layer**:

- **Domain layer.** Pure planning logic, organized as one namespace per pipeline stage plus an orchestrator:
  - `Schema/` — the schema model, desired/current providers, `ISchemaTransformer`, `ISchemaPolicy`.
  - `Diff/` — the structured diff model (`Diff/Model/`, rooted at `DatabaseDiff`), `ISchemaComparer`, `IDiffTransformer`, `IDiffPolicy`, and the diff renderer.
  - `Plan/` — the executable plan model (`Plan/Model/`, rooted at `MigrationPlan`), `IPlanLinearizer`, `IPlanTransformer`, `IPlanPolicy`.
  - `Migration/` — the orchestrator that runs the three stages: `IMigrationPlanner`, `MigrationPlanResult`, `MigrationOptions`. No knowledge of operations or how a run is orchestrated.
- **Application layer.** Orchestration of a run:
  - `Operations/` — one vertical slice per operation (`Operations/Plan/`, `Operations/Apply/`, `Operations/Refresh/`, `Operations/Import/`, `Operations/Validate/`, `Operations/Destroy/`), each holding a public interface (`IPlanOperation`, …), a public arguments record (`PlanArguments`, …, empty where the operation has no inputs yet), and the internal handler. Also `OperationOptions`, `IMigrationHelper` (schema resolution, planning, and state capture, under `Operations/Services/`), the default `IOperationReporter` (`DefaultOperationReporter`), the default `IOperationConfirmation` (`AutoApproveConfirmation`), and `IOperationConfirmation` itself (`Operations/Confirmation/`).

### Operations

Each operation is its own seam: a public `I{Name}Operation` interface with `Execute({Name}Arguments arguments, CancellationToken)`, an internal handler, and a public arguments record (the discoverable home for that operation's inputs). `NSchemaApplication` resolves the interface with `GetRequiredService<I{Name}Operation>()` and passes the arguments. There is no shared `IOperation` marker and no `OperationKind` enum — adding an operation means a new slice folder (interface + arguments + handler), a `TryAddSingleton` registration, and a public method (plus arguments overload) on `NSchemaApplication`.

Built-in operations (`src/NSchema.Core/Operations/`):

- **`PlanOperation`** — plans against the offline source (preferred), reports the plan and the SQL preview (if an `ISqlGenerator` dialect is registered). Does not execute.
- **`ApplyOperation`** — plans against the online source (required), generates SQL via `ISqlGenerator`, previews it, executes via `ISqlExecutor`, and captures state.
- **`RefreshOperation`** — captures the live schema to the state store without planning or applying.
- **`ImportOperation`** — fetches the live schema (optionally filtered by `ImportArguments.Schemas` / `ImportArguments.Tables`), then writes it to the configured `ISchemaImportTarget` via `IKeyedResolver<ISchemaImportTarget>.Current`. Import is additive: existing tables in the target are preserved.
- **`ValidateOperation`** — loads the desired schema and validates it against the registered `ISchemaPolicy` implementations, without planning or applying.
- **`DestroyOperation`** — tears down the managed schema. It reads the managed schema from the state store (offline) when one is configured, otherwise from the declared desired schema, and diffs it against an empty schema to produce drops, then generates SQL and executes it (online required), like `ApplyOperation`. Destroy uses `IMigrationPlanner.PlanTeardown`, a **trusted path that bypasses the diff/plan transformers and policies** (so a custom policy can't block teardown and a transformer can't silently alter it); the destructive-action policy therefore never runs. `IOperationConfirmation` still gates execution.

The shared orchestration used by `PlanOperation`, `ApplyOperation`, `DestroyOperation`, `RefreshOperation`, and `ValidateOperation` lives in **`IMigrationHelper`** (`src/NSchema.Core/Operations/Services/`): it collects desired providers, derives scope, fetches the current schema, calls `IMigrationPlanner`, reports the diff/plan, and (for `Refresh` / post-apply) captures state to the store.

### Confirmation

`IOperationConfirmation` (`src/NSchema.Core/Operations/Confirmation/`) gates execution of the destructive operations (`Apply`, `Destroy`). Its `Confirm` method receives an `OperationConfirmationRequest` — an abstract record carrying the `MigrationPlan` and an `IsDestructive` flag — with sealed `ApplyConfirmationRequest` and `DestroyConfirmationRequest` subtypes. A front-end can switch on the concrete type for a tailored prompt, or read `IsDestructive` to gate teardowns more strongly. The default `AutoApproveConfirmation` (in `Operations/`) approves everything without prompting.

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

### Planner

`DefaultMigrationPlanner` (`src/NSchema.Core/Migration/DefaultMigrationPlanner.cs`) is a pure domain service. It takes two pre-resolved `DatabaseSchema` values and produces a `MigrationPlanResult` (which carries the executable `MigrationPlan`, its structured `DatabaseDiff`, and any `PolicyDiagnostic`s). The **structured diff is the primary artifact**: the comparer emits it directly, and the linearizer derives the ordered plan from it. The pipeline is three stages, each of which transforms its representation and then validates it:

1. **Schema stage** — `ISchemaTransformer`s run in registration order against the desired schema, then every `ISchemaPolicy` validates it. A schema-policy error is fatal and skips the rest.
2. **Diff stage** — `ISchemaComparer` produces the structured `DatabaseDiff` directly (no flat-action intermediate). `IDiffTransformer`s then run in registration order, and every `IDiffPolicy` validates the result. The built-in `DestructiveActionDiffPolicy` runs here (it reasons over `ChangeKind.Remove` and narrowing column changes) and enforces `MigrationOptions.DestructiveActionPolicy`.
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

| Interface                                                 | Registered via                                                                                                            |
|-----------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------|
| `ISchemaProvider` (desired)                               | `AddSchema<T>()` / `AddSchemasFromAssembly[Containing]<T>()`                                                              |
| `ISchemaProvider` (online current)                        | `UseCurrentSchema<T>()` — typically called from a database-provider extension (e.g. `UsePostgres(...)`)                   |
| `I{Name}Operation` (one per operation)                    | `TryAddSingleton<I{Name}Operation, {Name}Operation>()` (built-in; replace before `Build()` to override)                  |
| `IOperationConfirmation`                                  | `Services.AddSingleton<IOperationConfirmation, T>()` (default `AutoApproveConfirmation`, registered with `TryAdd`)        |
| `ISchemaTransformer`                                      | `AddSchemaTransformer<T>()` / `AddSchemaTransformersFromAssembly[Containing]<T>()`                                        |
| `ISchemaPolicy`                                           | `AddSchemaPolicy<T>()`                                                                                                    |
| `IDiffTransformer`                                        | `AddDiffTransformer<T>()` / `AddDiffTransformersFromAssembly[Containing]<T>()`                                            |
| `IDiffPolicy`                                             | `AddDiffPolicy<T>()` / `AddDiffPoliciesFromAssembly[Containing]<T>()`                                                     |
| `IPlanTransformer`                                        | `AddPlanTransformer<T>()` / `AddPlanTransformersFromAssembly[Containing]<T>()`                                            |
| `IPlanPolicy`                                             | `AddPlanPolicy<T>()` / `AddPlanPoliciesFromAssembly[Containing]<T>()`                                                     |
| `IScriptProvider`                                         | `AddScripts(provider)` / `AddScriptFromFile(...)` / `AddScriptsFromEmbeddedResources(...)`                                |
| `ISqlExecutor`                                            | `UseSqlExecutor<T>()` (replaces default)                                                                                  |
| `ISchemaStateStore`                                       | `UseStateStore<T>()` / `UseStateStore(instance)` / `UseFileStateStore(path)`                                              |
| `IOperationReporter` (keyed by name)                      | `AddReporter<T>(format)` / `AddReporter(format, instance)` (last-wins per key); select via `OperationOptions.Reporter`    |
| `ISqlGenerator` (keyed by `Dialect`)                      | `AddSqlGenerator<T>(dialect)`, typically a database-provider extension; select with `WithDialect(...)`                    |
| `ISchemaSerializer` (keyed by `Format`)                   | `AddSchemaSerializer<T>(format)` (first-wins); `UseSchemaSerializer<T>(format)` to replace (JSON built-in)                |
| `ISchemaImportTarget` (keyed by name)                     | `AddImportTarget<T>(name)` / `AddFileImportTarget(opts => ...)` (last-wins per key); resolved per run by `ImportArguments.Target` |
| `ISchemaComparer`, `IPlanLinearizer`, `IMigrationPlanner` | Override via `Services.AddSingleton<...>()` before `Build()` (defaults registered with `TryAdd`)                          |
| `IDiffRenderer`                                           | `UseTerraformRenderer(...)` / `UseRenderer<TRenderer>()`, or override via `Services.AddSingleton<...>()` before `Build()` |
| `ISqlPlanRenderer`                                        | Override via `Services.AddSingleton<...>()` before `Build()` (default `DefaultSqlPlanRenderer`)                           |

### Resolving one of many (resolver pattern)

Several seams let you register multiple implementations and select one by key, sharing a single `IKeyedResolver<TValue>` interface (`Resolution/`) backed by DI keyed services. `IOperationReporter` (by reporter name), `ISqlGenerator` (by `Dialect`), and `ISchemaSerializer` (by `Format`) read the key for the current run from options (via `Current`). `ISchemaImportTarget` (by name) is instead resolved **explicitly** from `ImportArguments.Target` (via `Resolve(key)`), so it has no ambient `Current`. `IOperationReporter` and `ISchemaImportTarget` use last-wins registration (`Services.Replace`); `ISqlGenerator` and `ISchemaSerializer` use first-wins (`TryAddKeyedSingleton`), and `ISchemaSerializer` has a `UseSchemaSerializer<T>(format)` method to replace the built-in.

`IKeyedResolver<TValue>` is injected directly into consumers and exposes:
- `Current` — resolves the implementation for the current run's configured key (e.g. `OperationOptions.Reporter`, `SqlOptions.Dialect`). Throws if no key is configured or the key isn't registered.
- `HasCurrent` — returns `true` if `Current` would succeed; use this to guard optional seams (e.g. SQL generators).
- `Resolve(key)` / `TryResolve(key, out value)` — resolve by explicit key (how the import operation selects its target).

The `DefaultKeyedResolver<TValue, TOptions>` implementation reads the current key from `IOptions<TOptions>` via a selector delegate supplied at registration time; the import-target resolver is registered without a selector, so it has no `Current` and is used only via `Resolve`.

### Diff rendering

The structured `DatabaseDiff` (`Diff/Model/`: schema → table → columns/indexes/constraints/grants, each carrying a `ChangeKind` of `Add`/`Modify`/`Remove`) is produced directly by `ISchemaComparer` during planning and carried on `MigrationPlanResult.Diff`. The model is presentation-agnostic, and it is the semantic source of truth — `IPlanLinearizer` derives the executable plan from it, so a diff transformer that changes the tree also changes what executes.

Rendering is a single phase: **`IDiffRenderer`** (default `TerraformDiffRenderer`) turns a `DatabaseDiff` into text. The default emits a Terraform-style diff; an alternative (e.g. JSON) can be registered without touching the diff projection. Each renderer owns its own options POCO — the Terraform renderer reads `TerraformDiffRendererOptions.IncludeColour`, which defaults via its property initializer to `EnvironmentHelpers.SupportsColor` (on unless `NO_COLOR` is set or output is redirected). Override it via `UseTerraformRenderer(o => ...)`. The renderer itself never reads the environment.

`IOperationReporter.ReportDiff(DatabaseDiff)` is the seam: the reporter owns the `IDiffRenderer` and writes the rendered text to its output. `IMigrationHelper` hands `result.Diff` to the reporter (alongside `ReportPlan(result.Plan)`) — there is no separate diff-building step.

### Renaming

Schemas, tables, and columns support rename detection via the fluent `RenamedFrom(oldName)` method, which sets the `OldName` property on the domain model. The comparer matches on `OldName` when the current name isn't found.

### Configuration options

**`MigrationOptions`** (`NSchema.Migration`) — what to migrate:
- `DestructiveActionPolicy` — `Error` (default), `Warn`, or `Allow`. Enforced by `DestructiveActionDiffPolicy`. Configured via `WithDestructiveActionPolicy(...)`.

Schema scope is **not** an option — it's a per-invocation argument. `PlanArguments` / `ApplyArguments` / `ValidateArguments` carry a `Schemas` filter; when `null`, scope is derived from the desired schema. `IMigrationHelper` takes the scope as an explicit parameter rather than reading ambient options. (`Destroy` is unscoped — it always tears down the whole managed schema.)

**`NSchemaApplicationOptions`** (`NSchema`) — how the application is constructed and run. Passed to `CreateBuilder(options)` and registered as a singleton; its values are fixed at build time (`init`-only):
- `Args` / `ApplicationName` / `EnvironmentName` / `ContentRootPath` — consumed by the builder constructor to configure the underlying `HostApplicationBuilder`.
- `ExceptionBehavior` — `ReportAndThrow` (default) or `Throw`. Read by `NSchemaApplication` when an operation throws: `ReportAndThrow` reports via the resolved `IOperationReporter` before rethrowing, `Throw` just rethrows.

The operation to run is **not** an option — it's chosen by which method you call on the built `NSchemaApplication` (`Plan()` / `Apply()` / `Refresh()` / `Import()` / `Validate()` / `Destroy()`). Each method has a no-arguments overload and an overload taking that operation's arguments record (e.g. `Import(ImportArguments)`).

**`OperationOptions`** (`NSchema.Operations`) — output:
- `Reporter` — the `IOperationReporter` key to render with (defaults to `DefaultOperationReporter.ReporterName`, `"default"`). Configured via `WithOperationOptions(o => o.Reporter = ...)`; resolved through `IKeyedResolver<IOperationReporter>.Current`.

**`SqlOptions`** (`NSchema.Sql`) — SQL generation and execution:
- `Dialect` — the `ISqlGenerator` dialect to generate (must be set explicitly when a generator is registered). Configured via `WithDialect(...)`; resolved through `IKeyedResolver<ISqlGenerator>.Current`.
- `TransactionMode` — `Single` (default; whole plan in one transaction, with carve-outs for statements marked `RunOutsideTransaction`) or `None`. Configured via `WithTransactionMode(...)`.

**`ImportArguments`** (`NSchema.Operations.Import`) — per-invocation import inputs, passed to `Import(...)`:
- `Schemas` — optional `string[]` scope filter; only these schemas are fetched from the live database.
- `Tables` — optional `string[]` table filter applied after fetching.
- `Target` — the key of the registered `ISchemaImportTarget` to write to, resolved via `IKeyedResolver<ISchemaImportTarget>.Resolve(...)`.
