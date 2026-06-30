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

`NSchemaApplication.CreateBuilder()` returns an `NSchemaApplicationBuilder` (its surface is split across the `NSchemaApplicationBuilder.*.cs` partials: `.Schemas`, `.State`, `.Policies`, `.Configuration`), which uses a `HostApplicationBuilder` internally purely to compose configuration, logging, metrics, and DI. `Build()` produces an `NSchemaApplication` (a plain `IDisposable`, **not** an `IHost`). It does not run as a host: the app is a thin facade exposing `Services`, `Operations` (`INSchemaOperations`), `Locks` (`IStateLockCoordinator`), `CurrentSchema` (`ICurrentSchemaProvider`), and `PlanFile` (`IPlanFileWriter`). Every operation is reached through `app.Operations` — each method takes an `{Name}Arguments` record and returns `Task<Result<{Name}Result>>`, carrying success/failure and diagnostics as data rather than throwing. There is no `BackgroundService` and no host lifecycle. The app instance is reusable — it may be invoked multiple times across its lifetime.

### Layer separation

The codebase splits into a **domain layer** (pure planning) and an **application layer** (orchestrating a run):

- **Domain layer**, one namespace per pipeline stage:
  - `Schema/` — the schema model (`Schema/Model/`, rooted at `DatabaseSchema`), the DDL reader/writer/parser (`Schema/Ddl/`), the desired-state provider (`DesiredSchemaProvider`), the current-schema provider, and `ISchemaPolicy`.
  - `Diff/` — the structured diff model (`Diff/Model/`, rooted at `DatabaseDiff`), `ISchemaComparer`, `IDiffPolicy` (`Diff/Policies/`), and the diff reader (`DiffReader`, which projects a `DatabaseDiff` into a renderer-neutral `DiffDocument`).
  - `Plan/` — the executable plan model (`Plan/Model/`, rooted at `MigrationPlan`), `IPlanLinearizer`, the saved-plan-file machinery (`Plan/PlanFile/`), and `IMigrationPlanner` (default `DefaultMigrationPlanner`) returning `Result<PlannedMigration>` (the diff + plan pair; policy diagnostics ride on the `Result`). The planner knows nothing about operations or run orchestration.
  - `Sql/` — `ISqlGenerator` (dialect, provided by a provider package) and `ISqlExecutor`. Core ships no dialect.
- **Application layer:**
  - `Operations/` — one vertical slice per operation (see below).
  - `IMigrationWorkflow` (`Operations/Services/`) — the imperative shell operations share.
  - `Configuration/` — the generic config-in-SQL model (`ConfigBlock`/`ConfigValue`); Core only carries capability, the CLI interprets it.

`DiffReader` is a **public, stateless utility, not a DI service** — Core never consumes it; it exists for consumers (the CLI's presenter is its only user). It's a plain `new`-able class with a shared `.Default` singleton, so a caller reads a diff without touching the container. `Read(DatabaseDiff)` returns a `DiffDocument` — an ordered list of `DiffLine`s, each carrying its `ChangeKind` (null for a blank spacer), nesting `Depth`, and already-formatted `Text`, plus the aggregate `DiffSummary`. The document is **renderer-neutral**: a front-end folds it into colour, Markdown, or plain text by mapping each line's kind, never re-parsing rendered text. There is no `IDiffReader` interface and no `Use*` builder method. Core ships **no** schema or SQL-plan text renderer — those `SchemaRenderer`/`SqlPlanRenderer` dumps were trivial presentation with no reuse value and moved to the CLI; a consumer wanting to render those models (or a different diff format) writes its own.

### Operations

Each operation is its own vertical slice: an **internal** `IOperation<TArgs, TResult>` handler (`Execute(args, ct)`), a **public** `{Name}Arguments` record (the discoverable home for its inputs, empty where it has none yet), and a **public** `{Name}Result` record. Handlers are registered as `IOperation<{Name}Arguments, Result<{Name}Result>>` and invoked through the public `INSchemaOperations` facade (`app.Operations`) — not by user code resolving the handler. Operations return `Result`/`Result<T>` and never throw for expected outcomes, print, lock, or prompt: they narrate transient progress through `IProgress<OperationProgress>` and leave rendering, locking, and confirmation to the caller. There is no shared non-generic `IOperation` marker and no `OperationKind` enum — adding an operation means a new slice folder (handler + arguments + result), a `TryAddSingleton<IOperation<…>, …>()` registration, and a method on `INSchemaOperations` / `NSchemaOperations`.

Built-in operations (`src/NSchema.Core/Operations/`):

- **`PlanOperation`** (`Plan`) — computes a plan without executing. `PlanArguments.Target` (`PlanTarget`) selects what: `Recorded` (the default — a preview against recorded state, falling back to live); `Live` (against the live database, required — what `Apply` plans with); `Teardown` (a teardown of the managed schema, computed offline against recorded/desired and unscoped — the `terraform plan -destroy` analogue, bypassing diff/plan policies). `OutFile` also writes a **saved plan file** (forward or teardown; requires a generator).
- **`ApplyOperation`** (`Apply`) — generates SQL, previews, executes via `ISqlExecutor`, captures state. `ApplyArguments.PlanFile` instead **applies a saved plan file** verbatim (reads and executes the saved SQL exactly, no recompute). A saved teardown flows through this same path, so **tearing down is `Plan(Teardown)` → `Apply`** — there is no separate destroy operation.
- **`RefreshOperation`** — captures the live schema to the state store without planning or applying.
- **`ValidateOperation`** — loads the desired schema and validates it against registered `ISchemaPolicy` implementations; no planning/applying.
- **`DriftOperation`** — reads recorded (offline) state and live (online) schema and compares (direction recorded → live, so an out-of-band add reads as `Add`), returning the `DatabaseDiff`. Requires both a state store and a live provider. A pure observation — no policies run. The analogue of `terraform plan -refresh-only`.
- **`ImportOperation`** — fetches the live schema (optionally filtered by `Schemas`) and writes it under `OutputDirectory` as SQL DDL via `DdlWriter`, **one file per major object** (e.g. `app/tables/users.sql`, `app/routines/add_tax.sql`), with schema-level objects in a per-schema header (`app.sql`) and database-global extensions in top-level `extensions.sql`. Additive: absent objects are left in place, re-imported objects merge (incoming wins). The file-split/merge logic lives directly in the handler — no `ISchemaImportTarget` seam.
- **`DoctorOperation`** — runs read-only health checks against the configured infrastructure and reports the outcome of each.

There is no `Show`, `Destroy`, `PlanDestroy`, or `ForceUnlock` operation. Previewing and applying a teardown are `Plan(Teardown)` / `Apply` above; reading recorded state or a saved plan, and inspecting or force-releasing the lock, are thin front-end reads over the public seams — `app.CurrentSchema`, `app.PlanFile`, and `app.Locks` — not Core operations. Confirmation is likewise a caller concern (the CLI prompts); Core operations neither prompt nor lock.

**`IMigrationWorkflow`** (`Operations/Services/`) is the imperative shell around the pure planner shared by `Plan`, `Apply`, `Refresh`, and `Validate`: `ComputePlan(currentSource, required, schemas)` and `ComputeTeardown()` resolve the desired and current schemas and run `IMigrationPlanner` (returning `Result<PlannedMigration>`), `Validate()` runs just the schema policies, and `Refresh()` captures the live schema to the store, returning a `StateCapture?` (`null` when no store is configured). `Drift`, `Import`, and `Doctor` do **not** use the workflow — they talk to `ICurrentSchemaProvider` / `ISchemaComparer` / the importer directly. The workflow does not lock, confirm, or render.

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

`IStateLock` (`State/IStateLock.cs`) is the **backend seam** coordinating exclusive access to shared state. It is separate from `ISchemaStateStore`, but one backend may implement both. `Acquire(StateLockRequest, ct)` returns an `IStateLockHandle` and throws `StateLockedException` (carrying the holder's `StateLockInfo` when readable) when the lock is already held; `Release(ct)` drops whatever lock is held; `Peek(ct)` reads the holder without acquiring. The handle exposes `Info` and an idempotent `Release(ct)` — it is **not** `IAsyncDisposable`, so a caller can acquire a lock and never release it to hold it past the current process (a long-lived manual lock).

Front-ends never touch `IStateLock` directly; they go through `IStateLockCoordinator` (`app.Locks`), which wraps it: `Acquire(request, skipLock)` returns a `Result<IStateLockHandle>` (a failure carrying the holder's details when already locked; a no-op handle when there is no backend, or when `skipLock` is set — the latter carrying a warning naming the lock it ran past), and `Peek` / `Release` (force-release) mirror the backend. **Locking is caller-managed** — Core operations never lock; the CLI acquires the lock through `app.Locks` around the state-mutating runs it guards (apply, refresh, and destroy-via-apply) and releases it in a `finally`.

**The lock is registered automatically alongside the store.** `UseFileStateStore(path)` also registers a matching `FileStateLock` at `<path>.lock`; `UseStateStore<T>()` / `UseStateStore(instance)` register the same instance as the lock when the backend also implements `IStateLock`. An **explicit** lock choice (`UseStateLock*` / `UseFileStateLock`, tracked via a private `_explicitStateLock` flag) is never overridden by store registration, regardless of call order. With no store and no explicit lock, **no `IStateLock` is registered** and the coordinator's `Acquire` yields a no-op handle (there is no `NoOpStateLock`). (`FileStateLock` is a local-dev lock-file, not a distributed lock.)

### Planner pipeline

`DefaultMigrationPlanner` (`Plan/DefaultMigrationPlanner.cs`) is a pure domain service: it takes two pre-resolved `DatabaseSchema` values and produces a `Result<PlannedMigration>` — the `PlannedMigration` pairs the executable `MigrationPlan` with its structured `DatabaseDiff`, and any `PolicyDiagnostic`s ride on the `Result` (a blocked policy is a `Result.Failure`, which may still carry the `PlannedMigration` so the offending diff stays visible). The **structured diff is the primary artifact** — the comparer emits it directly, the linearizer derives the ordered plan from it. Three stages:

1. **Schema stage** — runs every `ISchemaPolicy` against the desired schema. A schema-policy error is fatal and skips the rest. Also exposed standalone as `IMigrationPlanner.Validate(desired)` (used by the validate operation).
2. **Diff stage** — `ISchemaComparer` produces the structured `DatabaseDiff` directly (no flat-action intermediate), and every `IDiffPolicy` validates it. The built-in `DestructiveActionDiffPolicy` runs here (reasoning over `ChangeKind.Remove` and narrowing column changes) and enforces `DestructiveActionOptions.Policy`.
3. **Plan stage** — `IPlanLinearizer` (default `DefaultPlanLinearizer`) walks the diff and emits actions in a safe dependency order. The planner attaches the deployment scripts to the `MigrationPlan` as `PreDeploymentScripts` / `PostDeploymentScripts` (scripts aren't a diff concept). At execution the script SQL is composed around the generated statements (pre first, post last) — scripts are raw SQL needing no dialect translation.

There are **no transformer seams**: the desired schema, diff, and plan are each validated by policies but never rewritten by the pipeline.
