# Changelog

All notable changes to NSchema will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

This release is a big rewrite converting `NSchema` package into `NSchema.Core` to allow the NSchema CLI tool to take the `NSchema` package name.

Alongside new reporting, diff rendering, and interactive controls, I've reorganized the codebase into clearer top-level namespaces and split SQL generation from execution so that plans can be previewed entirely offline. Output formats, SQL dialects, and schema file formats are now pluggable: you register several implementations and select one per run by key.

Planning and applying behavior are the same as before, but most public types have moved namespaces and a few have been renamed, so you'll need to update your `using` directives (and a handful of type names) when upgrading.

### Added

- A new `IOperationConfirmation` interface in `NSchema.Operations` that can be used to seek confirmation before an operation makes changes. This is intended for interactive scenarios (e.g. CLI) where the user can review the plan and confirm before proceeding.
- Exception handling can now be controlled via `NSchemaApplicationOptions.ExceptionBehavior` (passed to `CreateBuilder(...)`). The default behavior is preserved: exceptions will be reported to the `IOperationReporter` and then re-thrown.
- Schemas are now diffed into a structured, hierarchical model (`DatabaseDiff`) and a new `IDiffRenderer` interface renders it for the reporter.
- `UseTerraformRenderer(...)` to configure the default Terraform-style renderer.
- SQL previews are now structured too. `ISqlPlanRenderer` renders a `SqlPlan` to text, mirroring `IDiffRenderer`.
- `PolicyDiagnostics`, a collection type for policy results, with a `PolicyDiagnosticSeverity` of `Info`, `Warning`, or `Error`.
- Pluggable output formats. `IOperationReporter` now carries a `Format`, so several reporters can be registered with `AddReporter<T>(format)` / `AddReporter(format, instance)` and one chosen per run via `NSchemaApplicationOptions.Reporter`. The built-in human-readable `default` reporter remains the default.
- Selectable SQL dialects. `ISqlGenerator` now carries a `Dialect`, so several generators can be registered with `AddSqlGenerator<T>(dialect)` and one chosen per run via `WithDialect(...)` (`SqlOptions.Dialect`).
- Pluggable schema document formats. A new `ISchemaSerializer` reads and writes a desired-schema file format (JSON built-in); register more with `AddSchemaSerializer<T>(format)`. `FileSchemaProvider` now delegates parsing to one.
- `Import` operation. Reads the live database schema and writes it to the local filesystem as desired-schema source files. Triggered via `app.Import(...)`.
- `Validate` operation. Reads the desired schema and validates it against all registered `ISchemaPolicy` implementations. Triggered via `app.Validate(...)`.
- `Destroy` operation. Destroys all managed objects in the target database. Triggered via `app.Destroy(...)`. Use with extreme caution.
- `PlanDestroy` operation. Previews the teardown plan (the plan to drop the managed schema) without confirming, executing, or capturing state. Triggered via `app.PlanDestroy(...)`. The preview-half of `Destroy`, mirroring how `Plan` previews `Apply`.
- `Show` operation. Reads the recorded (offline) state from the state store and renders it, without planning or contacting the live database. Triggered via `app.Show(...)`. The schema-management analogue of `terraform show`; requires a configured state store.
- `Drift` operation. Compares the recorded (offline) state against the live database and reports how the live database has drifted from the recorded state, without planning or applying. Triggered via `app.Drift(...)`. The analogue of `terraform plan -refresh-only`; requires both a configured state store and a live database provider.
- `ISchemaRenderer`, the schema-side counterpart to `IDiffRenderer`: it renders a single `DatabaseSchema` as text (default `DefaultSchemaRenderer`, an indented tree). Replace it via `UseSchemaRenderer<T>()`. The `Show` operation renders through it via the new `IOperationReporter.ReportSchema(DatabaseSchema)`.
- State locking. A new `IStateLock` seam coordinates exclusive access to the state so concurrent state-mutating runs (`Apply`, `Destroy`, `Refresh`) can't race against the same state; a blocked acquire throws `StateLockedException`. The lock is **registered automatically alongside the state store**: `UseFileStateStore(path)` also wires a `FileStateLock` at `<path>.lock`, and a backend implementing both `ISchemaStateStore` and `IStateLock` is registered once for both. Override the co-located lock with `UseStateLock<T>()` / `UseStateLock(instance)` / `UseFileStateLock(path)`. With no store configured, locking is off (a no-op `NoOpStateLock`).
- `ForceUnlock` operation. Removes a stale state lock regardless of holder, reporting who held it. Triggered via `app.ForceUnlock(...)`; backed by `IStateLock.ForceUnlock`.
- A new set of structural and linting schema policies that include checks for common issues like missing primary keys or invalid indexes.

### Changed

- **Breaking:** Namespaces have been flattened. Several areas have been promoted out of the `NSchema.Migration` umbrella into top-level namespaces that mirror the architecture.
- **Breaking:** `NSchemaApplication` is no longer an `IHost` and no longer runs the migration as a `BackgroundService`. `Build()` returns a single-use object whose operation methods run the work synchronously and let exceptions propagate to the caller; there is no host lifecycle. Run behaviour (reporter, exception behaviour) is configured via `NSchemaApplicationOptions` passed to `CreateBuilder(...)`.
- **Breaking:** The single migration-operation seam has been replaced by one internal handler per operation, each with an `Execute(<Operation>Arguments, …)` method. Operations are invoked by calling the matching method on the built application (`app.Plan()`, `app.Apply()`, …); per-run inputs are passed via the public arguments records (`PlanArguments`, `ApplyArguments`, …). `IMigrationConfirmation` is now `IOperationConfirmation`.
- **Breaking:** Schema scoping is now a per-operation argument (`PlanArguments.Schemas` / `ApplyArguments.Schemas` / `ValidateArguments.Schemas`) rather than the ambient `MigrationOptions.SchemaNames` / `ForSchemas(...)`, which are removed.
- **Breaking:** The plan-stage extension points are now named for the stage, matching `ISchemaPolicy` / `IDiffPolicy`: `IMigrationPlanTransformer` is now `IPlanTransformer`, `IMigrationPolicy` is now `IPlanPolicy` (registered with `AddPlanPolicy<T>()`, was `AddMigrationPolicy<T>()`), and `IMigrationLinearizer` is now `IPlanLinearizer`. They live in `NSchema.Plan` alongside the `MigrationPlan` model.
- **Breaking:** `ISqlPlanner` is now `ISqlGenerator`, and its `Plan(MigrationPlan)` method is now `Generate(MigrationPlan)`. Register it with `AddSqlGenerator<T>(dialect)` (was `UseSqlPlanner<T>()`). It also now requires a `Dialect` property so generators can be selected by dialect.
- **Breaking:** `UseSqlGenerator<T>()` has been renamed to `AddSqlGenerator<T>(dialect)`, matching the other additive registration methods.
- **Breaking:** `IMigrationReporter` is now `IOperationReporter` (in `NSchema.Operations`). It also now requires a `Format` property so reporters can be selected by output format; custom reporters must supply one.
- **Breaking:** `FileSchemaProvider` is no longer abstract with a `Parse(Stream)` method; it now takes an `ISchemaSerializer`. Implement a new file format by implementing `ISchemaSerializer` rather than subclassing `FileSchemaProvider`.
- **Breaking:** `IOperationReporter.ReportPreview(IReadOnlyList<string>)` is now `ReportSqlPlan(SqlPlan)`, so the reporter receives the structured plan and renders it via `ISqlPlanRenderer` rather than a pre-flattened list of strings.
- **Breaking:** `IOperationReporter`'s `ReportPlan(MigrationPlan)` has been replaced by `ReportDiff(DatabaseDiff)`. The plan is converted to a structured diff before it is reported.
- **Breaking:** `IOperationReporter` gained a `ReportSchema(DatabaseSchema)` method, which presents a single schema state (used by the `Show` operation). Custom reporters must implement it.
- **Breaking:** `PolicyError` is now `PolicyDiagnostic`, and `PolicySeverity` is now `PolicyDiagnosticSeverity`. Custom `ISchemaPolicy` / `IPlanPolicy` implementations return `PolicyDiagnostic`s.
- **Breaking:** The `DestructiveActionPolicy` enum moved to `NSchema.Diff.Policies`, alongside the `DestructiveActionOptions` it configures (set via `WithDestructiveActionPolicy(...)`).
- **Breaking:** Most async surfaces now use `ValueTask` instead of `Task` for better performance in the common synchronous case.
- **Breaking:** `SqlType` is now a flat object instead of a class hierarchy, making serialization significantly easier, more robust and extensible. The built-in types are now static properties instead of subclasses.
- `DefaultSqlExecutor` no longer requires a `DbDataSource` to be constructed; it's an optional dependency, and execution throws a clear error if no connection is configured. This keeps the container wiring unconditional.
- Migration reporting messages have been overhauled to be more informative.
- The `IOperationReporter` now logs directly to the console instead of using `ILogger`. This removes some hacky wiring around segregating logging sinks by category.
- **Breaking:** `ISchemaStateStore` now deals in serialized state rather than `DatabaseSchema` snapshots. The core owns the state format and serializes/deserializes around the store.

### Removed

- **Breaking:** `IMigrationCompiler`, `ICompiledMigration`, and `UseMigrationCompiler<T>()`. SQL handling now relies on `ISqlGenerator` and `ISqlExecutor`.
- **Breaking:** `IMigrationPlanRenderer`. Plan output now flows through `IDiffRenderer` (diff → text); register a custom `IDiffRenderer` or call `UseTerraformRenderer(...)` instead.
- ** Breaking:** `ISchemaAggregator` has been removed. Instead, use `DatabaseSchema.Combine` to aggregate schemas.

### Fixed

- Toggling a column into or out of an identity column is now detected and emitted as a change. Previously only changes between two already-identity columns were picked up.
- Table privilege grants are now rendered by decomposing the privilege flags (e.g. `SELECT, INSERT`) instead of using the enum name, which could surface aliases like `ReadOnly` for `SELECT`.
- Fixed an issue with the schema domain models where deserializing them could leave collection properties as `null` instead of empty.
- Fixed an issue where exceptions thrown during a migration were being swallowed silently by the host.
- Fixed a regression where apply and refresh were scoping the final schema snapshot to the filtered schemas rather than the full set.

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

The file mirrors the schema model, with SQL types written as compact strings (`"int"`, `"varchar(255)"`, `"decimal(10,2)"`). Multiple files can be registered and are aggregated like any other provider. See [Defining schemas in JSON](docs/schemas.md#defining-schemas-in-json) for the format reference.

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
