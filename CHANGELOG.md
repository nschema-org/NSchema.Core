# Changelog

All notable changes to NSchema will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

> Versions before 3.0.0 covered the library-only era of NSchema. They are kept for historical reference only.

## [4.3.0] - 2026-07-09

### Added

- **Data migrations.** A `MIGRATION ['name'] FOR <trigger> <schema>.<table>.<member> AS $$…$$;` block attaches raw SQL to a structural change (`ADD COLUMN`, `ALTER COLUMN TYPE`, or `ADD CONSTRAINT`) and is spliced into the plan only when the matching change is planned. A block matching nothing is reported as an informational diagnostic and is safe to delete.
- **Decomposed NOT NULL adds.** A required column add with no default and a matching `FOR ADD COLUMN` migration is planned as add-nullable → run the migration SQL → `SET NOT NULL`, so the add succeeds against a populated table.
- **Hazard suppression.** A matching migration block silences the corresponding data-hazard diagnostic (per trigger; unique-index hazards have no trigger and are never suppressed).
- **`ExecuteDataMigration`** plan action carrying the spliced SQL. Executing a plan containing one requires a provider that recognizes it (4.3+); plans with no matched blocks are unaffected.

## [4.2.0] - 2026-07-09

### Added

- **Data-hazard detection.** A new built-in diff policy flags planned changes that are valid against the schema but can fail on the data already in a table.

## [4.1.0] - 2026-07-08

### Added

- **Schema template declaration.** A `TEMPLATE name BEGIN … END;` block allows you to declare a reusable group of objects once.
- **Schema template application.** A `APPLY TEMPLATE name IN SCHEMA a, b;` statement instantiates a template into each named schema.
- **Table templates.** A `TEMPLATE name FOR TABLE BEGIN … END;` block declares reusable table members.
- **Index name validation.** Duplicate index and index-backed constraint names (primary key, unique, exclusion) within a schema are now rejected at validation time.
- **`DdlDocument.Templates`** carries the parsed template constructs for consumers of the reader — a `TemplateSet` of definitions, applications, and includes.
- The formatter lays out `TEMPLATE` blocks canonically: header and `BEGIN`/`END` on their own lines, inner statements formatted as usual and indented one level.

## [4.0.1] - 2026-07-06

### Added

- **`ViewDiff.Materialized`.** A plain ⇄ materialized view conversion is now carried explicitly on the diff as a `ValueChange<bool>`, and `DiffReader` renders it as a label transition (`view → materialized view app.name`).

### Fixed

- **Renamed tables.** Constraint, index, and trigger drops (and privilege revokes) on a table being renamed now target the old table name, and execute before the rename.
- **Renamed schemas.** The schema rename is now ordered ahead of every other action, so drops and revokes inside the renamed schema resolve correctly.
- **Renamed materialized views.** In-place index drops on a renamed view target the old view name. A rename accompanying a recreate no longer emits a `RenameView` (which would have targeted a dropped view); the drop removes the old name and the recreate creates the new one.
- **View conversions.** A plain ⇄ materialized conversion now drops the view as what it currently is, instead of using the desired side's materialization for the drop.

## [4.0.0] - 2026-07-01

v4.0.0 is a major release that reworks providers and backends into a new plugin system. This will enable providers and backends to be installed directly from NuGet, independently of the CLI, and pin the versions in your CI.

### Added

- **Plugin Contract.** A new set of interfaces in `NSchema.Plugins` that will allow providers and backends to declare themselves.
- **BREAKING: Manual lock holds.** `IStateLockHandle` is no longer disposable; release is explicit, so a caller can take a lock that outlives the current process (e.g. one front-end command acquires, another releases) simply by never releasing it.
- **Lock time-to-live.** `StateLockRequest.TimeToLive` records an optional expiry on the resulting `StateLockInfo.ExpiresUtc`. Expiry is surfaced for visibility but never auto-enforced.
- **BREAKING: Caller-managed locking.** The state lock is acquired by the caller through `app.Locks` (`IStateLockCoordinator.Acquire(operation, skipLock, …)`) and held across the operations it guards, rather than each operation taking its own.
- **Schema-read seams on the application.** `app.CurrentSchema` (`ICurrentSchemaProvider`) reads the recorded (offline) or live (online) schema, and `app.PlanFile` (`IPlanFileWriter`) reads and writes saved plan files — exposed as properties alongside `app.Operations` / `app.Locks`.
- **BREAKING: Inspect and release the lock via the coordinator.** `IStateLockCoordinator` now manages the whole lock lifecycle through `app.Locks`: `Peek(ct)` reads the current `StateLockInfo?` without acquiring it, and `Release(ct)` force-releases whatever is held and returns the released lock's details.
- **BREAKING: Operation surface.** Every operation is reached through `app.Operations` (the `INSchemaOperations` facade) with a uniform `XArguments` → `Result<XResult>` shape, and each result carries its outcome as data.
- **BREAKING: Result & diagnostic model.** Operations no longer throw to signal expected outcomes or print their own output. They return `Result`/`Result<T>` carrying success/failure plus `Diagnostic`s, and narrate transient progress through `IProgress<OperationProgress>`; the caller decides what to render.
- **`UseProgressReporter<TProgress>()`.** Registers the `IProgress<OperationProgress>` sink that receives an operation's transient progress narration, replacing the default no-op reporter — a named builder method alongside `UseSqlGenerator`.
- **Atomic file-state writes.** The built-in file state store now writes to a temporary sibling file and atomically renames it into place, so a concurrent reader (e.g. a `plan` reading the recorded state while an `apply` captures new state) never observes a half-written snapshot.
- **Public diff reader.** `DiffReader` is a public, stateless utility (with a shared `.Default` instance) that reads a `DatabaseDiff` into a renderer-neutral `DiffDocument`.

### Changed

- **BREAKING: operations live on `app.Operations`, not `NSchemaApplication`.** `NSchemaApplication` is now a thin facade exposing `Services`, `Operations` (`INSchemaOperations`), `Locks` (`IStateLockCoordinator`), `CurrentSchema` (`ICurrentSchemaProvider`), and `PlanFile` (`IPlanFileWriter`); the per-operation methods (`Plan`, `Apply`, `Refresh`, `Validate`, `Drift`, `Import`, `Doctor`) moved onto `app.Operations`.
- **BREAKING: `IStateLockHandle` is no longer `IAsyncDisposable`.** Release is explicit via `ValueTask Release(CancellationToken)`, and the handle now exposes `StateLockInfo Info` (replacing `string LockId`). Operation call sites release in a `finally`.
- **BREAKING: `IStateLock.ForceUnlock` renamed to `IStateLock.Release`**, now returning `ValueTask` (was `Task<StateLockInfo?>`) to match `IStateLockHandle.Release`. Whether a release is "forced" is decided by the seam the caller reaches for — `IStateLock.Release` removes whatever lock is held; `IStateLockHandle.Release` removes only its own.
- **`StateLockInfo`** gains an optional `ExpiresUtc`.

### Removed

- **BREAKING: `NoOpStateLock` and the no-op lock fallback.** `IStateLock` is now registered only when a state backend supplies one; an operation either takes a real lock or runs without one, rather than acquiring a placeholder. Operations resolve `IStateLock?` (optional).
- **BREAKING: The `ForceUnlock` operation.** `NSchemaApplication.ForceUnlock`, `IForceUnlockOperation`, and `ForceUnlockArguments` are gone; force-unlock is a thin caller of `IStateLock.Release()` (the CLI does the peek + expected-id check + confirmation itself).
- **BREAKING: The `Show` operation.** `NSchemaApplication.Show`, `IShowOperation`, and `ShowArguments` are gone. Reading-and-rendering the recorded state, a saved plan, or (new) the live schema is a thin front-end concern, built on the public read seams above rather than a Core operation.
- **BREAKING: Renderer interfaces and `Use*Renderer` builder methods.** `IDiffRenderer`, `ISchemaRenderer`, and `ISqlPlanRenderer` are gone, along with `UseDiffRenderer<T>()`, `UseTerraformRenderer(…)`, `UseSchemaRenderer<T>()`, and `UseSqlPlanRenderer<T>()`. The renderers were never consumed by Core and had no swap points; they are now public concrete utilities (see Added) rather than DI-registered services. A consumer wanting a different format writes its own renderer and calls it directly.

## [3.4.0] - 2026-06-25

### Added

- **`Doctor` operation.** `NSchemaApplication.Doctor(DoctorArguments)` runs read-only health checks against the configured infrastructure.
- **`IStateLock.Peek`.** Reads the held lock (or `null` when free) without acquiring it, so a diagnostic never contends with a real operation. Added
  as a **default interface method** (returns `null`), so existing implementers are source-compatible.
- **Force-unlock by id.** `ForceUnlockArguments.ExpectedLockId` makes `ForceUnlock` a compare-and-swap: refused with a `StateLockMismatchException`
  unless it matches the held lock. Unset keeps the previous "remove whatever is held" behavior.

## [3.3.0] - 2026-06-24

### Changed

- Dropping a schema now emits specific drop instructions for all known elements beneath. This is required for providers that don't support cascading deletes.

## [3.2.1] - 2026-06-24

### Fixed

- Fixed a bug where trailing comments would get merged when formatting DDL. They should now be preserved.
- Fixed a bug where whitespace between comments and statements would get stripped when formatting DDL. It will now collapse to a single blank line.

## [3.2.0] - 2026-06-21

**More SQL Server Enhancements.** A second gap found while building the SQL Server provider: its triggers carry their action as an inline statement body, not by calling a separate function as PostgreSQL's do.

### Added

- `Trigger` now has an optional `Body` to take an statement body, alongside the existing `Function` that Postgres uses. The two are mutually exclusive: a trigger either executes a function, or runs an inline body (SQL Server). `Body` is optional and defaults to `null`, so the change is source-compatible, and it participates in structural equality (a body change is a drop + recreate, like any other structural trigger change).
- The SQL DDL parser and writer accept and emit the inline form: `CREATE TRIGGER … ON s.t AS $$ … $$` (dollar-quoted, so the body may contain its own `;`), in addition to the existing `… EXECUTE FUNCTION f(args)` form.

## [3.1.0] - 2026-06-21

**SQL Server Enhancements.** In working on the upcoming SQL Server provider, some functionality gaps were identified. This release goes towards enabling the SQL Server provider to work without hacks.

### Added

- When modifying a column's type or nullability, SQL Server requires restating the full column definition. To facilitate this, the `AlterColumnType` and `AlterColumnNullability` actions now include both the desired type and nullability. Both are optional and default to `null`, so the change is source-compatible. A modified column's `ColumnDiff.Definition` is now populated with the desired column, and the plan linearizer threads these final values onto the two actions.

## [3.0.0] - 2026-06-20

**First stable release.** This release is a ground-up rewrite, reworking the `NSchema` library into a thin CLI wrapper around a new `NSchema.Core`.

The full Terraform-style lifecycle (`plan` / `apply` / `destroy`) etc. has been implemented along with features like saved plans, drift detection, backend state and locking.

See the new documentation site for all details: https://nschema.dev.

## [2.1.0] - 2026-06-02

### Added

- Glob support for JSON schemas. `AddJsonSchemasFromGlob("schemas/**/*.json")` registers a provider for every matching file, and `AddJsonSchemasFromDirectory` now matches with the same globbing engine. Each file is aggregated like any other provider.
- `FileSchemaProvider`, a public base class for file-backed `ISchemaProvider`s. It handles file existence, stream lifetime, and schema-name filtering; derived providers implement only the format-specific `Parse`.
- `AddFileSchemasFromGlob` and `AddFileSchemasFromDirectory` builder methods, the shared file-discovery primitives behind the JSON helpers. Both take a provider factory so any file-backed provider can reuse the globbing.

## [2.0.1] - 2026-06-01

### Fixed

- Fixed a bug where trying to register multiple desired schema providers of the same concrete type would only resolve the first one.

### Changed

- `UseStateStoreFile` is now `UseFileStateStore` to align with the other extension methods. The old method still exists, it's just been marked obsolete.
- Schema filtering now uses a dedicated `Filter` method on the `DatabaseSchema` model for better reuse and discoverability.

## [2.0.0] - 2026-06-01

Version 2 focuses on improving developer experience with a more explicit and extensible model for planning and applying changes. It also introduces an optional Terraform-style state store so that plans can be made against snapshots rather than a live database.

### Added

#### Backend state store (new)

By default NSchema plans against the live database, but this isn't always possible. A CI pipeline may have no way to reach the database, or you may want plans to reflect the last deployed state rather than any drift since then.

NSchema now supports an optional state store that persists a snapshot of the schema after every successful apply:

```csharp
builder.UseStateStoreFile("schema_state.json");
```

Once a store is registered, `Plan` operations automatically read from it instead of the live database (offline planning), while `Apply` operations always use the live database. No further configuration is needed.

A new `Refresh` operation captures the current live schema to the store without planning or applying anything. Use this to initialize the store, or to record drift that happened outside of NSchema.

`FileSchemaStateStore` is a ready-made file-backed implementation. Custom stores implement `ISchemaStateStore`. Alongside this release, there will be an `NSchema.Aws` package with an implementation for S3.

#### JSON schemas (new)

Desired schemas can now be declared in a JSON file instead of C#, so you can describe a schema without a compiled project:

```csharp
builder.AddJsonSchema("schema.json");
```

The file mirrors the schema model, with SQL types written as compact strings (`"int"`, `"varchar(255)"`, `"decimal(10,2)"`). Multiple files can be registered and are aggregated like any other provider.

### Upgrading from 1.x

The API has changed significantly. This section is organised around what you need to do, depending on your role.

#### If you are a library user

**The default operation is now `Plan`.** NSchema will not apply changes unless you explicitly configure it with `RunOperation(MigrationOperation.Apply)` or call `app.Apply()`. This prevents accidental data loss when running NSchema for the first time.

- **`DryRun` / `DryRunOnly()` have been removed.** Use `RunOperation(MigrationOperation.Plan)` or `app.Plan()` instead.
- **`NSchemaApplication` now has explicit entry points.** `Plan()`, `Apply()`, and `Refresh()` methods that run a specific operation regardless of the configured default. `RunAsync()` still uses the configured operation.
- **`MigrationOptions` has been broken up.** Settings that control what gets migrated (`SchemaNames`, `DestructiveActionPolicy`) stay in `MigrationOptions`. Settings that control how a run executes (`Operation`, `TransactionMode`) have moved to `MigrationRunOptions` and `SqlExecutorOptions`. The builder methods still work as before; only direct reads of `IOptions<MigrationOptions>` need to change.
- **`PolicyError` has a new `Severity` property.** The existing 2-argument constructor still compiles, but custom `IMigrationPolicy` implementations should use `PolicySeverity.Warning` to signal non-fatal findings rather than returning errors.

#### If you are a database provider

- **`IMigrationReporter` has moved** to the `NSchema.Migration` namespace (was `NSchema.Hosting`). Update any `using` directives.
- **`IMigrationExecutor` and `UseMigrationExecutor<T>()` have been removed.** Implement `IMigrationCompiler` instead. A compiler receives a `MigrationPlan` and returns an executable `ICompiledMigration` unit. Register it with `UseMigrationCompiler<T>()`.
- **`UseCurrentSchema<T>()` is unchanged.** It still registers the live database provider. No action required.
- **`IMigrationPlanner` is now public** and its `Plan()` method now takes explicit `DatabaseSchema currentSchema` and `DatabaseSchema desiredSchema` parameters. If you have a custom planner implementation, update the signature. The planner is now a pure domain service — it no longer resolves schema providers from DI.

## [1.0.1] - 2026-05-28

### Fixed

- Fixed a bug where primary keys, foreign keys, and indexes weren't being displayed in the reported plan for new tables.

## [1.0.0] - 2026-05-27

First stable release. The public API is now covered by semantic versioning. Breaking changes will only ship in a new major version.

### Added

- Declarative schema definition via `AbstractSchemaProvider` and the fluent `Schema` / `Table` / `Column` / `Index` / `ForeignKey` builders.
- Hosted application model: `NSchemaApplication.CreateBuilder(...)` produces an `IHost`-backed app that runs the migration as a `BackgroundService`.
- A single `ISchemaProvider` interface used for both desired-state and current-state schemas, with the role determined at DI registration time (`AddSchema<T>()` / `UseSchemaSource<T>()`).
- Pluggable pipeline with extension points for every stage: `ISchemaPolicy`, `IMigrationPlanTransformer`, `IMigrationPolicy`, `IScriptProvider`, `ISqlPlanner`, `ISqlExecutor`, `IMigrationExecutor`.
- Rename detection for schemas, tables, and columns via `RenamedFrom(...)`.
- Migration options: `DestructiveActionPolicy` (`Error` / `Warn` / `Allow`), `DryRun`, `TransactionMode` (`Single` / `None`) with per-statement `RunOutsideTransaction` carve-outs, and `SchemaNames` scope filter via `ForSchemas(...)`.
- Built-in `ActionOrderingTransformer` (topological ordering of plan actions) and `DestructiveActionMigrationPolicy`.
- Pre- and post-deployment script support via `IScriptProvider`, `AddScriptFromFile(...)`, and `AddScriptsFromEmbeddedResources(...)`.
- SourceLink and symbol packages (`.snupkg`) published alongside the main package for source-level debugging.

[4.0.0-beta.2]: https://github.com/nschema-org/NSchema.Core/compare/v3.1.0...v4.0.0-beta.2
[3.1.0]: https://github.com/nschema-org/NSchema.Core/compare/v3.0.0...v3.1.0
[3.0.0]: https://github.com/nschema-org/NSchema.Core/compare/v2.1.0...v3.0.0
[2.1.0]: https://github.com/nschema-org/NSchema.Core/compare/v2.0.1...v2.1.0
[2.0.1]: https://github.com/nschema-org/NSchema.Core/compare/v2.0.0...v2.0.1
[2.0.0]: https://github.com/nschema-org/NSchema.Core/compare/v1.0.1...v2.0.0
[1.0.1]: https://github.com/nschema-org/NSchema.Core/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/nschema-org/NSchema.Core/releases/tag/v1.0.0
