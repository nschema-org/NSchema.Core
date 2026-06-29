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

# Run a single test method
dotnet test tests/NSchema.Core.Tests --filter "FullyQualifiedName~DefaultSchemaComparerTests.Compare_AddsTable"
```

Targets `net10.0`. Warnings are errors in the test project. The solution (`NSchema.Core.slnx`) contains only `src/NSchema.Core` and `tests/NSchema.Core.Tests`. The Postgres provider was extracted to its own repository (commit `dd3e695`); only Core lives here.

### Tests

xUnit v3 with **Shouldly** (assertions), **NSubstitute** (mocks), **Verify.XunitV3** (snapshots), and **Testcontainers.PostgreSQL** (real-database integration tests under `tests/.../EndToEnd`, which spin up a Postgres container even though the provider lives elsewhere). `using` directives for `Xunit`/`Shouldly`/`NSubstitute`/`VerifyXunit` are global.

**Snapshot tests are the convention for every output surface** (diff renderer, plan renderer, DDL writer, state serialization, …). They live next to the test as `*.verified.txt` in a `Snapshots/` folder. When intended output changes, update the snapshot (Verify writes a `*.received.txt` on mismatch; accept it to update the `*.verified.txt`).

## Architecture

NSchema is a declarative database schema migration library for .NET (the engine behind the NSchema CLI). The user describes the schema they want in **SQL DDL**; NSchema introspects the database, diffs, and applies the difference.

`NSchemaApplication.CreateBuilder()` returns an `NSchemaApplicationBuilder` (its surface is split across the `NSchemaApplicationBuilder.*.cs` partials: `.Schemas`, `.State`, `.Policies`, `.Configuration`), which uses a `HostApplicationBuilder` internally purely to compose configuration, logging, metrics, and DI. `Build()` produces an `NSchemaApplication` (a plain `IDisposable`, **not** an `IHost`). It does not run as a host: calling `Plan()`/`PlanDestroy()`/`Apply()`/`Refresh()`/`Import()`/`Validate()`/`Destroy()`/`Show()`/`Drift()`/`ForceUnlock()` resolves that operation's dedicated interface (`IPlanOperation`, `IApplyOperation`, …) from DI and `await`s its `Execute(arguments, ct)` directly, passing the matching arguments record. Exceptions propagate to the caller. There is no `BackgroundService` and no host lifecycle. The app instance is reusable — it may be invoked multiple times across its lifetime.

### Layer separation

The codebase splits into a **domain layer** (pure planning) and an **application layer** (orchestrating a run):

- **Domain layer**, one namespace per pipeline stage:
  - `Schema/` — the schema model (`Schema/Model/`, rooted at `DatabaseSchema`), the DDL reader/writer/parser (`Schema/Ddl/`), the desired-state provider (`DesiredSchemaProvider`), the current-schema provider, and `ISchemaPolicy`.
  - `Diff/` — the structured diff model (`Diff/Model/`, rooted at `DatabaseDiff`), `ISchemaComparer`, `IDiffPolicy` (`Diff/Policies/`), and the diff renderer (`DiffRenderer`).
  - `Plan/` — the executable plan model (`Plan/Model/`, rooted at `MigrationPlan`), `IPlanLinearizer`, the saved-plan-file machinery (`Plan/PlanFile/`), and `IMigrationPlanner` (default `DefaultMigrationPlanner`) returning `Result<PlannedMigration>` (the diff + plan pair; policy diagnostics ride on the `Result`). The planner knows nothing about operations or run orchestration.
  - `Sql/` — `ISqlGenerator` (dialect, provided by a provider package), `ISqlExecutor`, and the SQL plan renderer (`SqlPlanRenderer`). Core ships no dialect.
- **Application layer:**
  - `Operations/` — one vertical slice per operation (see below).
  - `IMigrationWorkflow` (`Operations/Services/`) — the imperative shell operations share.
  - `Configuration/` — the generic config-in-SQL model (`ConfigBlock`/`ConfigValue`); Core only carries capability, the CLI interprets it.

The three text renderers (`DiffRenderer`, `SchemaRenderer`, `SqlPlanRenderer`) are **public, stateless utilities, not DI services** — Core never consumes them; they exist for consumers (the CLI's presenter is their only user). Each is a plain `new`-able class with a shared `.Default` singleton, so a caller renders without touching the container. They emit plain text only (`DiffRenderer` takes an optional `DiffRendererOptions` carrying just the nesting `Indent`); a colour-aware front-end styles the `+`/`-`/`~` markers itself. There are no `IDiffRenderer`/`ISchemaRenderer`/`ISqlPlanRenderer` interfaces and no `Use*Renderer` builder methods — a consumer wanting a different format writes its own.

### Operations

Each operation is its own seam: an **internal** `I{Name}Operation` interface with `Execute({Name}Arguments arguments, CancellationToken)`, an internal handler, and a **public** arguments record (the discoverable home for that operation's inputs, empty where it has none yet). `NSchemaApplication` resolves the interface with `GetRequiredService<I{Name}Operation>()` and passes the arguments. Interfaces are internal because operations are invoked via the public methods on `NSchemaApplication`, not by user code resolving the handler; only the arguments records are public. There is no shared `IOperation` marker and no `OperationKind` enum — adding an operation means a new slice folder (interface + arguments + handler), a `TryAddSingleton` registration, and a public method (plus arguments overload) on `NSchemaApplication`.

Built-in operations (`src/NSchema.Core/Operations/`):

- **`PlanOperation`** — plans against the offline source (preferred), reports the plan and SQL preview (if a generator is registered). Does not execute. `PlanArguments.OutFile` also writes a **saved plan file** (requires a generator).
- **`PlanDestroyOperation`** — previews the teardown plan via the same trusted `PlanTeardown` path `Destroy` uses (bypassing diff/plan transformers and policies). The preview-half of `Destroy`, mirroring how `Plan` previews `Apply`. The analogue of `terraform plan -destroy`. `OutFile` writes a saved teardown plan.
- **`ApplyOperation`** — plans against the online source (required), generates SQL, previews, executes via `ISqlExecutor`, captures state. `ApplyArguments.PlanFile` instead **applies a saved plan file** without recomputing (reads, confirms, executes the saved SQL exactly). A saved teardown flows through this same path, so there is no apply-from-file path on `Destroy`. The analogue of `terraform apply <planfile>`.
- **`RefreshOperation`** — captures the live schema to the state store without planning or applying.
- **`ImportOperation`** — fetches the live schema (optionally filtered by `Schemas`) and writes it under `OutputDirectory` as SQL DDL via `DdlWriter`, **one file per major object** (e.g. `app/tables/users.sql`, `app/routines/add_tax.sql`), with schema-level objects in a per-schema header (`app.sql`) and database-global extensions in top-level `extensions.sql`. Additive: absent objects are left in place, re-imported objects merge (incoming wins). The file-split/merge logic lives directly in the handler — no `ISchemaImportTarget` seam.
- **`ValidateOperation`** — loads the desired schema and validates it against registered `ISchemaPolicy` implementations, no planning/applying.
- **`DestroyOperation`** — tears down the managed schema (read offline from the state store when configured, else from declared desired), diffs against an empty schema, generates and executes drops (online required). Uses `IMigrationPlanner.PlanTeardown`, a **trusted path that bypasses diff/plan transformers and policies** (no custom policy can block teardown, no transformer can alter it), so the destructive-action policy never runs. `IOperationConfirmation` still gates execution.
- **`ShowOperation`** — reports the recorded (offline) state via `IOperationReporter.ReportSchema`, no planning or live contact. Requires a state store. `ShowArguments.PlanFile` instead reads a **saved plan file** via `IPlanFileWriter` and reports its diff/plan/SQL — the read-only counterpart of `apply --plan-file` (`terraform show <planfile>`).
- **`DriftOperation`** — reads recorded (offline) state and live (online) schema, compares (direction recorded → live, so an out-of-band add reads as `Add`), reports the `DatabaseDiff`. Requires both a state store and a live provider. A pure observation — no transformers/policies run, never fails on a policy violation. The analogue of `terraform plan -refresh-only`.
- **`ForceUnlockOperation`** — gates on `IOperationConfirmation` (a destructive `ForceUnlockConfirmationRequest`), then calls `IStateLock.ForceUnlock` to remove a stale lock regardless of holder, and reports who held it. No-op under the default `NoOpStateLock`.

**`IMigrationWorkflow`** (`Operations/Services/`) is the imperative shell around the pure planner used by `Plan`, `PlanDestroy`, `Apply`, `Destroy`, `Refresh`, and `Validate`: it collects desired providers, derives scope, fetches the current schema, calls the planner, reports the diff/plan, and captures state. State capture goes through `Refresh(RefreshMode)`: `Required` (the `Refresh` operation) throws with no store; `Optional` (post-apply / post-destroy) silently no-ops with no store. `Show` and `Drift` do **not** use the workflow — they are thin read-only inspections that talk to `ICurrentSchemaProvider` (and `ISchemaComparer` / `IPlanFileWriter`) directly.

### Confirmation

`IOperationConfirmation` (`Operations/Confirmation/`) gates `Apply`, `Destroy`, and `ForceUnlock`. Its `Confirm` receives an `OperationConfirmationRequest` — abstract, carrying an `IsDestructive` flag — with sealed `ApplyConfirmationRequest` / `DestroyConfirmationRequest` (each carrying the `MigrationPlan`) and `ForceUnlockConfirmationRequest` (no plan) subtypes. A front-end switches on the concrete type for a tailored prompt or reads `IsDestructive`. The default `AutoApproveConfirmation` approves everything.

### Schema providers

**Desired-state** schema comes exclusively from SQL DDL files. `AddDdlSchemas(baseDirectory, glob|Matcher)` registers a `DdlSchemaSource`; the internal `IDesiredSchemaProvider` (`DesiredSchemaProvider`) globs every registered source, reads matched files with `DdlReader`, and aggregates into a `DesiredProject` — the desired `DatabaseSchema` **plus** the deployment scripts declared inline (PRE/POST DEPLOYMENT `$$…$$` blocks). `GetProject(scope, ct)` is its one method; there is no in-code desired-schema seam and no `ISchemaTransformer`. Multiple `AddDdlSchemas` calls aggregate (e.g. a base set plus an environment overlay).

The DDL stack lives in `Schema/Ddl/`: `DdlLexer` → `DdlParser` (partials per statement family: `.Create`, `.Drop`, `.Grant`, `.Config`, `.Scripts`, `.SchemaAccumulator`) → schema model. `DdlWriter` renders the model back to DDL (used by Import). `DdlFormatter` is a gentle token-stream reformatter (backs the CLI `fmt` command) — it pretty-prints without going through the full parse/model round-trip, so it preserves what it doesn't understand.

**Current-state** access goes through `ICurrentSchemaProvider`. The live source implements the public `ISchemaProvider`, used **only** for current-state:

```csharp
ValueTask<DatabaseSchema> GetSchema(SchemaSourceMode preferred, string[]? schemaNames, bool required = true, CancellationToken cancellationToken = default);
```

- `Online` — reads the live database provider (registered via `UseCurrentSchema<T>()`). Throws if unconfigured.
- `Offline` — reads the state-backed source (requires a registered `ISchemaStateStore`). Throws if unconfigured.
- `Offline, required: false` — prefers offline, falls back to online, throws only if neither exists.

The internal `DefaultCurrentSchemaProvider` wires both. Registering a state store via `UseStateStore*()` is all that's needed to enable offline planning.

`ISchemaStateStore` deals only in **serialized payloads** (`Read`/`Write` of `ReadOnlyMemory<byte>`) — a persistence sink (file, blob, …) that never sees the schema model. Core owns the format: it serializes via the internal `ISchemaStateSerializer` before writing (in `IMigrationWorkflow.Refresh`) and deserializes after reading (in `StateBackedSchemaProvider`). A custom store only loads/saves an opaque byte payload.

### State locking

`IStateLock` (`State/IStateLock.cs`) coordinates exclusive access to shared state so two state-mutating operations can't run concurrently. It is a **separate seam** from `ISchemaStateStore`, but one backend may implement both. `Acquire(StateLockRequest, ct)` returns an `IStateLockHandle` (`IAsyncDisposable`; dispose to release). When held, `Acquire` throws `StateLockedException` (carrying the holder's `StateLockInfo` when readable). `ForceUnlock(ct)` removes the current lock regardless of holder.

The three **state-mutating** operations acquire the lock for the whole run via `await using`: `Apply`, `Destroy`, `Refresh`. Read-only operations (`Plan`, `PlanDestroy`, `Show`, `Drift`, `Validate`) never lock.

**The lock is registered automatically alongside the store.** `UseFileStateStore(path)` also registers a matching `FileStateLock` at `<path>.lock`; `UseStateStore<T>()` / `UseStateStore(instance)` register the same instance as the lock when the backend also implements `IStateLock`. Store registration never overrides an **explicit** lock choice (`UseStateLock*` / `UseFileStateLock`), regardless of call order (tracked via a private `_stateLockConfigured` flag). With no store and no explicit lock, the default `NoOpStateLock` makes acquisition a no-op. (`FileStateLock` is a local-dev lock-file, not a distributed lock.)

### Planner pipeline

`DefaultMigrationPlanner` (`Plan/DefaultMigrationPlanner.cs`) is a pure domain service: it takes two pre-resolved `DatabaseSchema` values and produces a `Result<PlannedMigration>` — the `PlannedMigration` pairs the executable `MigrationPlan` with its structured `DatabaseDiff`, and any `PolicyDiagnostic`s ride on the `Result` (a blocked policy is a `Result.Failure`, which may still carry the `PlannedMigration` so the offending diff stays visible). The **structured diff is the primary artifact** — the comparer emits it directly, the linearizer derives the ordered plan from it. Three stages:

1. **Schema stage** — runs every `ISchemaPolicy` against the desired schema. A schema-policy error is fatal and skips the rest. Also exposed standalone as `IMigrationPlanner.Validate(desired)` (used by the validate operation).
2. **Diff stage** — `ISchemaComparer` produces the structured `DatabaseDiff` directly (no flat-action intermediate), and every `IDiffPolicy` validates it. The built-in `DestructiveActionDiffPolicy` runs here (reasoning over `ChangeKind.Remove` and narrowing column changes) and enforces `DestructiveActionOptions.Policy`.
3. **Plan stage** — `IPlanLinearizer` (default `DefaultPlanLinearizer`) walks the diff and emits actions in a safe dependency order. The planner attaches the deployment scripts to the `MigrationPlan` as `PreDeploymentScripts` / `PostDeploymentScripts` (scripts aren't a diff concept). At execution the script SQL is composed around the generated statements (pre first, post last) — scripts are raw SQL needing no dialect translation.

There are **no transformer seams**: the desired schema, diff, and plan are each validated by policies but never rewritten by the pipeline.
