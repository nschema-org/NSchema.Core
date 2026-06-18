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

NSchema is a declarative database schema migration library for .NET. The user describes the schema they want in DDL (see *Defining a schema* below); NSchema introspects the database, diffs, and applies the difference.

`NSchemaApplication.CreateBuilder()` returns an `NSchemaApplicationBuilder`, which uses a `HostApplicationBuilder` internally purely to compose configuration, logging, metrics, and the DI container. `Build()` produces an `NSchemaApplication` (a plain `IDisposable`, not an `IHost`). It does **not** run as a host: calling `Plan()`/`PlanDestroy()`/`Apply()`/`Refresh()`/`Import()`/`Validate()`/`Destroy()`/`Show()`/`Drift()`/`ForceUnlock()` resolves that operation's dedicated interface (`IPlanOperation`, `IApplyOperation`, …) from DI and `await`s its `Execute(arguments, ct)` directly, passing the matching arguments record. Exceptions propagate to the caller. There is no `BackgroundService` and no host lifecycle. The app is single-run — a second invocation throws.

### Layer separation

The codebase is split into a **domain layer** and an **application layer**:

- **Domain layer.** Pure planning logic, organized as one namespace per pipeline stage:
  - `Schema/` — the schema model, the desired-state DDL reader (`DesiredSchemaProvider`), the current-schema provider, and `ISchemaPolicy`.
  - `Diff/` — the structured diff model (`Diff/Model/`, rooted at `DatabaseDiff`), `ISchemaComparer`, `IDiffPolicy`, and the diff renderer.
  - `Plan/` — the executable plan model (`Plan/Model/`, rooted at `MigrationPlan`), `IPlanLinearizer`, and the planner that runs all three stages: `IMigrationPlanner` (default `DefaultMigrationPlanner`) and its `MigrationPlanResult`. The planner has no knowledge of operations or how a run is orchestrated.
- **Application layer.** Orchestration of a run:
  - `Operations/` — one vertical slice per operation (`Operations/Plan/`, `Operations/PlanDestroy/`, `Operations/Apply/`, `Operations/Refresh/`, `Operations/Import/`, `Operations/Validate/`, `Operations/Destroy/`, `Operations/Show/`, `Operations/Drift/`, `Operations/ForceUnlock/`), each holding a public interface (`IPlanOperation`, …), a public arguments record (`PlanArguments`, …, empty where the operation has no inputs yet), and the internal handler. Also `IMigrationWorkflow` (the imperative shell operations share — schema resolution, planning, and state capture, under `Operations/Services/`), the default `IOperationReporter` (`DefaultOperationReporter`), the default `IOperationConfirmation` (`AutoApproveConfirmation`), and `IOperationConfirmation` itself (`Operations/Confirmation/`).

### Operations

Each operation is its own seam: an **internal** `I{Name}Operation` interface with `Execute({Name}Arguments arguments, CancellationToken)`, an internal handler, and a **public** arguments record (the discoverable home for that operation's inputs). `NSchemaApplication` resolves the interface with `GetRequiredService<I{Name}Operation>()` and passes the arguments. The interfaces are internal because operations are invoked via the public methods on `NSchemaApplication` (`Plan()`, `Apply()`, …), not by user code resolving the handler; only the arguments records are public. There is no shared `IOperation` marker and no `OperationKind` enum — adding an operation means a new slice folder (interface + arguments + handler), a `TryAddSingleton` registration, and a public method (plus arguments overload) on `NSchemaApplication`.

Built-in operations (`src/NSchema.Core/Operations/`):

- **`PlanOperation`** — plans against the offline source (preferred), reports the plan and the SQL preview (if an `ISqlGenerator` dialect is registered). Does not execute. When `PlanArguments.OutFile` is set, it also writes a **saved plan file** (requires a generator) — see *Saved plan files* below.
- **`PlanDestroyOperation`** — previews the teardown plan: calls `IMigrationWorkflow.PlanDestroy` (the same trusted `PlanTeardown` path `Destroy` uses, bypassing the diff/plan transformers and policies) and reports the SQL preview (if a generator is registered). The preview-half of `Destroy`, mirroring how `Plan` previews `Apply`. Does not confirm, execute, or capture state. The analogue of `terraform plan -destroy`. When `PlanDestroyArguments.OutFile` is set, it writes a saved plan file of the teardown.
- **`ApplyOperation`** — plans against the online source (required), generates SQL via `ISqlGenerator`, previews it, executes via `ISqlExecutor`, and captures state. When `ApplyArguments.PlanFile` is set, it instead **applies a saved plan file** without recomputing: it reads the file, confirms, and executes the saved SQL exactly as reviewed. A saved teardown plan flows through this same path (a saved plan is just a plan), so there is no apply-from-file path on `Destroy`. The analogue of `terraform apply <planfile>`.
- **`RefreshOperation`** — captures the live schema to the state store without planning or applying.
- **`ImportOperation`** — fetches the live schema (optionally filtered by `ImportArguments.Schemas`), then writes it to the local filesystem under `ImportArguments.OutputDirectory` as SQL DDL via `DdlWriter`, **one file per major object**: each table/view/routine goes to its own `.sql` file grouped by type under a per-schema directory (e.g. `app/tables/users.sql`, `app/routines/add_tax.sql`), and the remaining schema-level objects (enums, sequences, grants, comment) share a per-schema header file (e.g. `app.sql`). Database-global **extensions** (not schema-scoped) go to a single top-level `extensions.sql`. There is no partition-mode choice — this object layout is the only one. Import is additive: each object is its own file, so objects absent from a later import are left in place, and a re-imported object is merged into its file (incoming wins). The file-writing logic (object split + additive merge) lives directly in the handler — there is no `ISchemaImportTarget` seam.
- **`ValidateOperation`** — loads the desired schema and validates it against the registered `ISchemaPolicy` implementations, without planning or applying.
- **`DestroyOperation`** — tears down the managed schema. It reads the managed schema from the state store (offline) when one is configured, otherwise from the declared desired schema, and diffs it against an empty schema to produce drops, then generates SQL and executes it (online required), like `ApplyOperation`. Destroy uses `IMigrationPlanner.PlanTeardown`, a **trusted path that bypasses the diff/plan transformers and policies** (so a custom policy can't block teardown and a transformer can't silently alter it); the destructive-action policy therefore never runs. `IOperationConfirmation` still gates execution.
- **`ShowOperation`** — reads the recorded (offline) state from the state store and reports it via `IOperationReporter.ReportSchema`, without planning or contacting the live database. The analogue of `terraform show`. Requires a state store (offline read throws otherwise). When `ShowArguments.PlanFile` is set, it instead reads that **saved plan file** via `IPlanFileWriter` and reports its diff, plan, and SQL (the same view the plan step produced) — no state store or live database needed. The analogue of `terraform show <planfile>`; the read-only counterpart of `apply --plan-file`.
- **`DriftOperation`** — reads the recorded (offline) state and the live (online) schema, compares them with `ISchemaComparer` (direction recorded → live, so an out-of-band add reads as `Add`, an out-of-band drop as `Remove`), and reports the resulting `DatabaseDiff`. The analogue of `terraform plan -refresh-only`. Requires both a state store and a live provider. The comparison is a pure observation — no diff/plan transformers or policies run — so it never fails on a policy violation.
- **`ForceUnlockOperation`** — gates on `IOperationConfirmation` (a `ForceUnlockConfirmationRequest`, which is `IsDestructive`), then calls `IStateLock.ForceUnlock` to remove a stale state lock regardless of holder, and reports who held it. The analogue of `terraform force-unlock` (which also prompts before overriding the lock). Does not plan, apply, or touch the schema. No-op (reports "no lock held") under the default `NoOpStateLock`.

The shared orchestration used by `PlanOperation`, `PlanDestroyOperation`, `ApplyOperation`, `DestroyOperation`, `RefreshOperation`, and `ValidateOperation` lives in **`IMigrationWorkflow`** (`src/NSchema.Core/Operations/Services/`) — the imperative shell around the pure `IMigrationPlanner`: it collects desired providers, derives scope, fetches the current schema, calls the planner, reports the diff/plan, and captures state to the store. State capture goes through `Refresh(RefreshMode)`: `Required` (the `Refresh` operation) throws when no store is configured, `Optional` (post-apply / post-destroy) is a silent no-op when there's no store. `ShowOperation` and `DriftOperation` do **not** use the workflow — they are thin read-only inspections that talk to `ICurrentSchemaProvider` (and, for drift, `ISchemaComparer`; for showing a saved plan, `IPlanFileWriter`) directly, since there is no desired schema to resolve, no plan to linearize, and no state to capture.

### Confirmation

`IOperationConfirmation` (`src/NSchema.Core/Operations/Confirmation/`) gates `Apply`, `Destroy`, and `ForceUnlock` before each makes a change worth reviewing. Its `Confirm` method receives an `OperationConfirmationRequest` — an abstract record carrying only an `IsDestructive` flag — with sealed `ApplyConfirmationRequest` / `DestroyConfirmationRequest` (each carrying the `MigrationPlan`) and `ForceUnlockConfirmationRequest` (no plan — there is none) subtypes. The plan lives on the plan-bearing subtypes rather than the base, since force-unlock has no plan to show. A front-end can switch on the concrete type for a tailored prompt, or read `IsDestructive` to gate teardowns and lock overrides more strongly. The default `AutoApproveConfirmation` (in `Operations/`) approves everything without prompting.

### Schema providers

**Desired-state** schema comes exclusively from SQL DDL files. `AddDdlSchemas(baseDirectory, glob|Matcher)` registers a `DdlSchemaSource` (a base directory + matcher); the internal `IDesiredSchemaProvider` (`DesiredSchemaProvider`) globs every registered source, reads the matched files with `DdlReader`, and aggregates them into a single `DesiredProject` — the desired `DatabaseSchema` **plus** the deployment scripts declared inline in those files. `GetProject(scope, ct)` is its one method; there is no in-code desired-schema seam and no `ISchemaTransformer`. Multiple `AddDdlSchemas` calls aggregate (e.g. a base set plus an environment overlay).

**Current-state** schema access goes through `ICurrentSchemaProvider` (`src/NSchema.Core/Schema/ICurrentSchemaProvider.cs`). The live source implements the public `ISchemaProvider` (`src/NSchema.Core/Schema/ISchemaProvider.cs`), which is now used **only** for current-state sources:

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

`DefaultMigrationPlanner` (`src/NSchema.Core/Plan/DefaultMigrationPlanner.cs`) is a pure domain service. It takes two pre-resolved `DatabaseSchema` values and produces a `MigrationPlanResult` (which carries the executable `MigrationPlan`, its structured `DatabaseDiff`, and any `PolicyDiagnostic`s). The **structured diff is the primary artifact**: the comparer emits it directly, and the linearizer derives the ordered plan from it. The pipeline is three stages:

1. **Schema stage** — the planner runs every `ISchemaPolicy` against the desired schema. A schema-policy error is fatal and skips the rest. This stage is also exposed on its own as `IMigrationPlanner.Validate(desired)`, which the validate operation uses.
2. **Diff stage** — `ISchemaComparer` produces the structured `DatabaseDiff` directly (no flat-action intermediate), and every `IDiffPolicy` validates the result. The built-in `DestructiveActionDiffPolicy` runs here (it reasons over `ChangeKind.Remove` and narrowing column changes) and enforces its own `DestructiveActionOptions.Policy`.
3. **Plan stage** — `IPlanLinearizer` (default `DefaultPlanLinearizer`) walks the diff and emits the migration actions in a safe dependency order. The planner then attaches the deployment scripts to the `MigrationPlan` as its `PreDeploymentScripts` / `PostDeploymentScripts` (scripts aren't a diff concept, so they live on the plan rather than in `Actions`). At execution time the script SQL is composed around the generated statements (pre first, post last) — scripts are raw SQL and need no dialect translation.

(There are no transformer seams: the desired schema, diff, and plan are each validated by policies but never rewritten by the pipeline.)

The planner has no knowledge of operations, online/offline routing, or the application/operation options.

### SQL generation and execution

Generating SQL and executing it are deliberately separate, so a plan can be previewed offline:

- **`ISqlGenerator`** (`UseSqlGenerator<T>()`, typically supplied by a database-provider extension) turns a `MigrationPlan` into a structured `SqlPlan` (an ordered list of `SqlStatement`s, each flagged if it `RunOutsideTransaction`). This is pure string-building — no connection — so the SQL preview works offline whenever a generator is registered. There is one generator per run; operations inject it directly as an optional `ISqlGenerator?`. The generator is optional: with none registered it is `null`, and `PlanOperation` reports the plan without a SQL preview.
- **`ISqlExecutor`** (default `DefaultSqlExecutor`) executes the `SqlPlan` against a `DbDataSource`. It reads `SqlOptions.TransactionMode` to decide whether to wrap everything in one transaction. It is the only online step.

The operations consume these two seams directly — there is no separate compiler abstraction. The `SqlPlan` reaches the reporter as a structured value via `IOperationReporter.ReportSqlPlan(SqlPlan)`, which renders it through **`ISqlPlanRenderer`** (default `DefaultSqlPlanRenderer`), mirroring the diff side's `IDiffRenderer`.

### Saved plan files

A plan can be saved to a file and applied later, unchanged — the analogue of `terraform plan -out` + `terraform apply <planfile>`. This closes the gap where a re-plan at apply time might diverge from what was reviewed. The artifact lives in `Plan/PlanFile/`:

- **`PlanFileEnvelope`** (internal) carries the structured `DatabaseDiff`, the `MigrationPlan`, the generated `SqlPlan`, and a `CreatedAt`. Storing the diff and plan (for display + confirmation) alongside the SQL (executed as-is) means applying from a file renders the same diff/plan/SQL view the plan step produced. Its `Version` is an `init` property defaulting to `CurrentVersion` — the format owns the version, so callers construct the envelope without supplying one and the writer validates it on read.
- **`IPlanFileWriter`** (default `PlanFileWriter`, registered with `TryAddSingleton`) is the single seam the operations depend on. It owns the JSON format (serialize/deserialize) *and* the file I/O (`Read` / `Write`) in one class — the serializer is not split out, since there's one consumer. It reuses `DomainModelJson.IgnoreComputedProperties` and configures **polymorphic** serialization for the `MigrationAction` hierarchy (discriminator `$action` = the concrete type name, discovered by reflection so a new action type needs no registration); polymorphism is set on the contract resolver, not via attributes, keeping the domain model serialization-free. `PlanOperation`, `PlanDestroyOperation`, and `ApplyOperation` inject this one dependency. Saving requires a registered generator (to produce the `SqlPlan`); the plan + diff come from the workflow's `PlannedMigration` result.
- The workflow's `Plan` / `PlanDestroy` return a **`PlannedMigration`** (`Operations/Services/`) — the `MigrationPlan` plus the `DatabaseDiff` it was derived from — so the plan operations can persist the diff (the workflow already reported it; this just surfaces it to the caller).
- Applying (`ApplyOperation.ApplyFromFile`, triggered by `ApplyArguments.PlanFile`) does **not** recompute the plan: it reads the file, takes the `"apply"` lock, reports the saved diff/plan/SQL, confirms with `ApplyConfirmationRequest`, executes the saved SQL, and captures state. A saved teardown is just a saved plan and flows through the same path. Plan files are local file paths, not routed through `ISchemaStateStore` — they're ephemeral CI artifacts with a different lifecycle from state.

### Defining a schema

Schemas are defined using SQL DDL. Declarative `CREATE` statements describing desired state (no `ALTER`). DDL text is read by the public, stateless `DdlReader` (`DdlReader.Instance.Read(source)` — a thin facade over the internal single-use `DdlParser`) and written by the public, stateless `DdlWriter` (`DdlWriter.Instance.Write(schema)`), both in `Schema/Ddl/`; the full grammar lives in `docs/ddl-grammar.md`. The old fluent `AbstractSchemaProvider` builder API and the JSON schema format have both been **removed** — the DSL is the only file input format.

```sql
CREATE SCHEMA app;

CREATE TABLE app.users
(
    id bigint NOT NULL IDENTITY,
    name text NOT NULL,
    CONSTRAINT users_pkey PRIMARY KEY (id)
);
```

DSL files are registered via `AddDdlSchemas(baseDirectory, globPattern)`, which records a `DdlSchemaSource` (the base directory + a matcher). The base directory is the matcher root and `globPattern` (defaulting to `**/*.sql`) is relative to it — so there is no glob-splitting logic; a wildcard-free pattern simply names a single file. There is also an `AddDdlSchemas(baseDirectory, Matcher)` overload taking a fully-configured `Microsoft.Extensions.FileSystemGlobbing.Matcher` directly, so callers can add **excludes** (e.g. the CLI excludes its environment-overlay files from the base set); the string overload just builds a one-include `Matcher` and delegates. **`AddDdlSchemas` may be called more than once** — the sources aggregate (e.g. a base set plus an environment overlay). The internal `IDesiredSchemaProvider` (`DesiredSchemaProvider`) evaluates every source's matcher **when the project is read** (not at registration), reads each matched file once with `DdlReader`, and combines them into a `DesiredProject` (schema + inline deployment scripts) — so the file set reflects the filesystem at plan/apply time. When **no source matches any file** it throws (`FileNotFoundException`): planning against an empty desired schema would read as "drop everything", so an unmatched set of sources is a configuration error. There is no in-code desired-schema seam.

#### Config-in-SQL

DSL files may also carry top-level **configuration blocks** — `NSCHEMA ( … )`, `BACKEND file ( … )`, `PROVIDER postgres ( … )` — orchestration metadata (dialect, state backend, live provider) declared alongside the schema, à la Terraform's `terraform`/`provider` blocks but in SQL-statement form (mirroring Postgres `WITH (option = value, …)`). The core **captures but never interprets** them: `DdlReader.Instance.Read(source)` returns a `DdlDocument` (schema + `IReadOnlyList<ConfigBlock>`). A front-end reads the blocks from `Read(source).Config` (and the desired schema from `.Schema`) — `DdlReader` and `DdlDocument` are public for exactly this. `ConfigBlock` / `ConfigValue` (in `NSchema.Configuration`) are a generic, flat model (`Type`, optional `Label`, `key = value` attributes; nesting via dotted keys). Interpretation — precedence (CLI > env > config > defaults), mapping a block to builder registration, provider dispatch, secrets-from-env — lives in the front-end/CLI, not the core.

### Extension points

| Interface                               | Registered via                                                                                                                  |
|-----------------------------------------|---------------------------------------------------------------------------------------------------------------------------------|
| SQL DDL files (desired schema)          | `AddDdlSchemas(baseDirectory, glob)` / `AddDdlSchemas(baseDirectory, Matcher)` (may be called more than once; sources aggregate) |
| `ISchemaProvider` (online current)      | `UseCurrentSchema<T>()` — typically called from a database-provider extension (e.g. `UsePostgres(...)`)                         |
| `I{Name}Operation` (one per operation)  | `TryAddSingleton<I{Name}Operation, {Name}Operation>()` (built-in; replace before `Build()` to override)                         |
| `IOperationConfirmation`                | `Services.AddSingleton<IOperationConfirmation, T>()` (default `AutoApproveConfirmation`, registered with `TryAdd`)              |
| `ISchemaPolicy`                         | `AddSchemaPolicy<T>()`                                                                                                          |
| `IDiffPolicy`                           | `AddDiffPolicy<T>()`                                                                                                            |
| `ISchemaStateStore`                     | `UseStateStore<T>()` / `UseStateStore(instance)` / `UseFileStateStore(path)`                                                    |
| `IStateLock`                            | auto co-located with the store; override with `UseStateLock<T>()` / `UseStateLock(instance)` / `UseFileStateLock(path)`         |
| `IOperationReporter`                    | `UseReporter<T>()` / `UseReporter(instance)` (last-wins; replaces the default `DefaultOperationReporter`)                        |
| `ISqlGenerator`                         | `UseSqlGenerator<T>()`, typically a database-provider extension (one generator per run; optional)                               |
| `IDiffRenderer`                         | `UseTerraformRenderer(...)` / `UseDiffRenderer<TRenderer>()`                                                                    |
| `ISchemaRenderer`                       | `UseSchemaRenderer<TRenderer>()` (default `DefaultSchemaRenderer`)                                                              |
| `ISqlPlanRenderer`                      | `UseSqlPlanRenderer<TRenderer>()` (default `DefaultSqlPlanRenderer`)                                                            |

The planning algorithm and aggregators — `ISchemaComparer`, `IPlanLinearizer`, `IMigrationPlanner`, `ICurrentSchemaProvider`, `IDesiredSchemaProvider` — and the state serializer (`ISchemaStateSerializer`) are **internal**. They remain interfaces for DI wiring and test mocking, but they are not extension points and not replaceable from user code. (The per-operation handler interfaces are internal too.)

### One implementation per run

Every replaceable seam resolves to a **single** implementation per run, injected directly — there is no keyed-resolver indirection. The replaceable ones (`IOperationReporter`, `ISqlGenerator`, `IDiffRenderer`, `ISchemaRenderer`, `ISqlPlanRenderer`, the state store/lock, …) are registered with `TryAddSingleton`/`Services.Replace` and overridden via the matching `Use*` method (last-wins). A seam with no default — currently only `ISqlGenerator` — is injected as a nullable optional dependency (`ISqlGenerator? = null`), so an operation guards it with a null check and falls back (e.g. `PlanOperation` reports the plan without a SQL preview when none is registered). Schema reading/writing isn't a seam at all — there is one format, SQL DDL, handled directly by `DdlReader` / `DdlWriter`.

### Diff rendering

The structured `DatabaseDiff` (`Diff/Model/`: schema → table → columns/indexes/constraints/grants, each carrying a `ChangeKind` of `Add`/`Modify`/`Remove`) is produced directly by `ISchemaComparer` during planning and carried on `MigrationPlanResult.Diff`. The model is presentation-agnostic, and it is the semantic source of truth — `IPlanLinearizer` derives the executable plan from it, so a diff transformer that changes the tree also changes what executes.

Rendering is a single phase: **`IDiffRenderer`** (default `TerraformDiffRenderer`) turns a `DatabaseDiff` into text. The default emits a Terraform-style diff; an alternative (e.g. JSON) can be registered without touching the diff projection. Each renderer owns its own options POCO — the Terraform renderer reads `TerraformDiffRendererOptions.IncludeColour`, which defaults via its property initializer to `EnvironmentHelpers.SupportsColor` (on unless `NO_COLOR` is set or output is redirected). Override it via `UseTerraformRenderer(o => ...)`. The renderer itself never reads the environment.

`IOperationReporter.ReportDiff(DatabaseDiff)` is the seam: the reporter owns the `IDiffRenderer` and writes the rendered text to its output. `IMigrationWorkflow` hands `result.Diff` to the reporter (alongside `ReportPlan(result.Plan)`) — there is no separate diff-building step.

There is a parallel seam for rendering a **single schema state** (rather than a change between two): **`ISchemaRenderer`** (default `DefaultSchemaRenderer`, an indented schema → table → columns/pk/fks/indexes/grants tree) turns a `DatabaseSchema` into text, and `IOperationReporter.ReportSchema(DatabaseSchema)` is its seam — the reporter owns the `ISchemaRenderer` and writes the rendered text out, mirroring the diff side. The `Show` operation reports through it. Replace the renderer via `UseSchemaRenderer<TRenderer>()`.

### Renaming

Schemas, tables, and columns support rename detection via the DSL's `RENAMED FROM <old>` clause, which sets the `OldName` property on the domain model. The comparer matches on `OldName` when the current name isn't found.

### Configuration options

**`DestructiveActionOptions`** (`NSchema.Diff.Policies`) — owned by the diff policy it configures:
- `Policy` — a `DestructiveActionPolicy`: `Error` (default), `Warn`, or `Allow`. Enforced by `DestructiveActionDiffPolicy` (which reads `IOptions<DestructiveActionOptions>`). Configured via `WithDestructiveActionPolicy(...)`.

Schema scope is **not** an option — it's a per-invocation argument. `PlanArguments` / `ApplyArguments` carry a `Schemas` filter; when `null`, scope is derived from the desired schema. `IMigrationWorkflow.Plan` takes the scope as an explicit parameter rather than reading ambient options. (`Destroy` is unscoped — it always tears down the whole managed schema. `Validate` is unscoped too — it always validates the whole desired schema, since partial validation would give false confidence and hide cross-schema policy violations.)

**`NSchemaApplicationOptions`** (`NSchema`) — how the application is constructed and run. Passed to `CreateBuilder(options)` and registered as a singleton; its values are fixed at build time (`init`-only):
- `Args` / `ApplicationName` / `EnvironmentName` / `ContentRootPath` — consumed by the builder constructor to configure the underlying `HostApplicationBuilder`.
- `ExceptionBehavior` — `ReportAndThrow` (default) or `Throw`. Read by `NSchemaApplication` when an operation throws: `ReportAndThrow` reports via the resolved `IOperationReporter` before rethrowing, `Throw` just rethrows.

The reporter to render with is **not** an option — there is one `IOperationReporter` per run, registered directly (default `DefaultOperationReporter`) and replaced via `UseReporter<T>()` / `UseReporter(instance)`. Operations and `NSchemaApplication` inject it directly, not through a resolver.

The operation to run is **not** an option — it's chosen by which method you call on the built `NSchemaApplication` (`Plan()` / `PlanDestroy()` / `Apply()` / `Refresh()` / `Import()` / `Validate()` / `Destroy()` / `Show()` / `Drift()` / `ForceUnlock()`). Each method takes that operation's arguments record (e.g. `Import(ImportArguments)`).

**`SqlOptions`** (`NSchema.Sql`) — SQL execution:
- `TransactionMode` — `Single` (default; whole plan in one transaction, with carve-outs for statements marked `RunOutsideTransaction`) or `None`. Configured via `WithTransactionMode(...)`.

The SQL generator is **not** an option — there is one `ISqlGenerator` per run, registered directly via `UseSqlGenerator<T>()` (typically by a database-provider extension) and injected into operations as an optional `ISqlGenerator?`.

**`ImportArguments`** (`NSchema.Operations.Import`) — per-invocation import inputs, passed to `Import(...)`:
- `Schemas` — optional `string[]` scope filter; only these schemas are fetched from the live database. Import scopes by namespace only — there is no object-level filter; the per-object file layout makes post-import curation (delete the files you don't want; re-import merges additively) the way to select individual objects.
- `OutputDirectory` — the root directory to write into (defaults to `.`). The import always uses the per-object layout described under `ImportOperation` above: one file per major object grouped by type under a per-schema directory, plus a per-schema header file for the schema-level objects. There is no `OutputFile` and no partition-mode option.

The output is always written as SQL DDL via `DdlWriter` (there is no format choice — JSON was removed); files use the `.sql` extension.
