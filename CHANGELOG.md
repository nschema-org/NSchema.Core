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
- `Import` operation. Reads the live database schema and writes it to the local filesystem as desired-schema source files. Triggered via `app.Import(...)`.
- `Validate` operation. Reads the desired schema and validates it against all registered `ISchemaPolicy` implementations. Triggered via `app.Validate(...)`.
- `Destroy` operation. Destroys all managed objects in the target database. Triggered via `app.Destroy(...)`. Use with extreme caution.
- `PlanDestroy` operation. Previews the teardown plan (the plan to drop the managed schema) without confirming, executing, or capturing state. Triggered via `app.PlanDestroy(...)`. The preview-half of `Destroy`, mirroring how `Plan` previews `Apply`.
- `Show` operation. Reads the recorded (offline) state from the state store and renders it, without planning or contacting the live database. Triggered via `app.Show(...)`. The schema-management analogue of `terraform show`; requires a configured state store.
- `Drift` operation. Compares the recorded (offline) state against the live database and reports how the live database has drifted from the recorded state, without planning or applying. Triggered via `app.Drift(...)`. The analogue of `terraform plan -refresh-only`; requires both a configured state store and a live database provider.
- `ISchemaRenderer`, the schema-side counterpart to `IDiffRenderer`: it renders a single `DatabaseSchema` as text (default `DefaultSchemaRenderer`, an indented tree). Replace it via `UseSchemaRenderer<T>()`. The `Show` operation renders through it via the new `IOperationReporter.ReportSchema(DatabaseSchema)`.
- State locking. A new `IStateLock` seam coordinates exclusive access to the state so concurrent state-mutating runs (`Apply`, `Destroy`, `Refresh`) can't race against the same state; a blocked acquire throws `StateLockedException`. The lock is **registered automatically alongside the state store**: `UseFileStateStore(path)` also wires a `FileStateLock` at `<path>.lock`, and a backend implementing both `ISchemaStateStore` and `IStateLock` is registered once for both. Override the co-located lock with `UseStateLock<T>()` / `UseStateLock(instance)` / `UseFileStateLock(path)`. With no store configured, locking is off (a no-op `NoOpStateLock`).
- `ForceUnlock` operation. Removes a stale state lock regardless of holder, reporting who held it. Triggered via `app.ForceUnlock(...)`; backed by `IStateLock.ForceUnlock`.
- Saved plan files. `Plan` and `PlanDestroy` can write the computed plan to a file via `PlanArguments.OutFile` / `PlanDestroyArguments.OutFile`, and `Apply` can execute a saved file via `ApplyArguments.PlanFile` instead of recomputing, so what was reviewed is exactly what is applied. The file stores the structured diff, the plan, and the generated SQL; applying from it reports the same diff/plan/SQL view and runs the saved SQL.
- A new set of structural and linting schema policies that include checks for common issues like missing primary keys or invalid indexes.
- View support. The schema model now includes views, declared in DDL with `CREATE VIEW s.v AS <query>` (and `DROP VIEW s.v`). A view's defining query is stored verbatim; the objects it reads are derived from the query's `FROM`/`JOIN` clauses (sub-queries and CTEs included) and drive ordering.
- Enum type support. The schema model now includes enum types, declared in DDL with `CREATE ENUM s.e ('a', 'b')` (and `DROP ENUM s.e`). Values are ordered, and evolution is additions-only: new values may be inserted anywhere (planned as anchored add-value actions), while a removal or reorder is reported in the diff but rejected at planning by the always-on `enum-value-removal` policy — the type must be recreated manually. Enums are created before, and dropped after, the tables that may use them.
- `INamedObject` (`Name`/`Comment`) and `IRenameableObject : INamedObject` (adds `OldName`), implemented by every named model record — table members (constraints, indexes) implement the former, renameable objects (tables, columns, views, enums, sequences, functions, procedures, schemas) the latter. On the diff side, `INamedObjectDiff` (`Name`/`Kind`) and `ISchemaObjectDiff : INamedObjectDiff` (adds `Schema`/`RenamedFrom`), with `SchemaDiff.EnumerateObjects()` and `TableDiff.EnumerateMembers()` enumerating changes across kinds. These back two shared comparer skeletons — matching, rename detection, and partial-schema drop semantics for schema-level objects, and the name-matched remove/add/comment algorithm for table list members — plus kind-agnostic diff consumers (change summary, destructive-change detection). No behavioral change.
- Function and procedure support. The schema model now includes functions and procedures, declared in DDL with `CREATE FUNCTION s.f(args) <definition>` / `CREATE PROCEDURE s.p(args) <definition>` (and `DROP FUNCTION` / `DROP PROCEDURE`). The argument list and definition are stored verbatim and compared for cosmetic equivalence (the same literal-aware normalization views use); definitions may contain dollar-quoted bodies (`$$ … $$` or `$tag$ … $tag$`) with embedded semicolons. Overloading is not supported (one routine per name), and functions and procedures share one name space per schema, as they do in the database. A definition-only change replaces in place (`CREATE OR REPLACE` semantics); an argument-list change plans a drop + recreate via a single `RecreateFunction`/`RecreateProcedure` action, since replacing under a different signature would orphan the old overload. Routines are created before, and dropped after, the tables whose defaults and checks may call them; views order around them for free.
- Sequence support. The schema model now includes standalone sequences, declared in DDL with `CREATE SEQUENCE s.q (AS bigint, START 100, INCREMENT 5, MINVALUE 1, MAXVALUE 999999, CACHE 10, CYCLE)` (and `DROP SEQUENCE s.q`) — the option style mirrors a column's `IDENTITY (…)` clause, and every option is optional. Option changes plan an `AlterSequence`. Sequences are created before, and dropped after, the tables whose defaults may use them.
- The NSchema DDL, now the primary way to declare a desired schema. The public, stateless `DdlReader` (`DdlReader.Instance.Read(source)`) reads declarative `CREATE SCHEMA` / `CREATE TABLE` (with inline, always-named constraints and inline indexes), `GRANT`, and the view/enum/sequence/function/procedure statements above from `.sql` files — it describes desired state, never `ALTER` steps, and is dialect-agnostic (canonical type names mapped to a dialect by `ISqlGenerator`). Doc-comments (`--- …`) become catalog comments, `RENAMED FROM` drives rename detection, and `CREATE PARTIAL SCHEMA` leaves undeclared tables alone. Register files with `AddSqlSchema(path)` / `AddSqlSchemasFromGlob(pattern)` / `AddSqlSchemasFromDirectory(dir)`. Columns may reference schema-qualified user-defined types (e.g. an enum as `app.status`). See `docs/ddl-grammar.md` for the full grammar.
- Config-in-SQL. DDL files may carry top-level configuration blocks: `NSCHEMA ( … )`, `BACKEND file ( … )`, `PROVIDER postgres ( … )` declaring orchestration metadata (dialect, state backend, live provider) alongside the schema, in SQL-statement form (mirroring Postgres `WITH (option = value, …)`). The core captures them into a generic `ConfigBlock` / `ConfigValue` model (`NSchema.Configuration`) but never interprets them: `DdlReader.Instance.Read(source)` returns a `DdlDocument` whose `Config` surfaces them for a front-end (and whose `Schema` is the desired schema). Interpretation — precedence, mapping a block to builder registration, provider dispatch, secrets-from-env — is a front-end concern.

### Changed

- **Breaking:** Namespaces have been flattened. Several areas have been promoted out of the `NSchema.Migration` umbrella into top-level namespaces that mirror the architecture.
- **Breaking:** `NSchemaApplication` is no longer an `IHost` and no longer runs the migration as a `BackgroundService`. `Build()` returns a single-use object whose operation methods run the work synchronously and let exceptions propagate to the caller; there is no host lifecycle. Run behaviour (reporter, exception behaviour) is configured via `NSchemaApplicationOptions` passed to `CreateBuilder(...)`.
- **Breaking:** The single migration-operation seam has been replaced by one internal handler per operation, each with an `Execute(<Operation>Arguments, …)` method. Operations are invoked by calling the matching method on the built application (`app.Plan()`, `app.Apply()`, …); per-run inputs are passed via the public arguments records (`PlanArguments`, `ApplyArguments`, …). `IMigrationConfirmation` is now `IOperationConfirmation`.
- **Breaking:** Schema scoping is now a per-operation argument (`PlanArguments.Schemas` / `ApplyArguments.Schemas` / `ValidateArguments.Schemas`) rather than the ambient `MigrationOptions.SchemaNames` / `ForSchemas(...)`, which are removed.
- **Breaking:** The plan-stage extension points are now named for the stage, matching `ISchemaPolicy` / `IDiffPolicy`: `IMigrationPlanTransformer` is now `IPlanTransformer`, `IMigrationPolicy` is now `IPlanPolicy` (registered with `AddPlanPolicy<T>()`, was `AddMigrationPolicy<T>()`), and `IMigrationLinearizer` is now `IPlanLinearizer`. They live in `NSchema.Plan` alongside the `MigrationPlan` model.
- **Breaking:** `ISqlPlanner` is now `ISqlGenerator`, and its `Plan(MigrationPlan)` method is now `Generate(MigrationPlan)`. Register it with `AddSqlGenerator<T>(dialect)` (was `UseSqlPlanner<T>()`). It also now requires a `Dialect` property so generators can be selected by dialect.
- **Breaking:** `UseSqlGenerator<T>()` has been renamed to `AddSqlGenerator<T>(dialect)`, matching the other additive registration methods.
- **Breaking:** `IMigrationReporter` is now `IOperationReporter` (in `NSchema.Operations`). It also now requires a `Format` property so reporters can be selected by output format; custom reporters must supply one.
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
- **Breaking:** The fluent `AbstractSchemaProvider` schema-builder API (the `NSchema.Schema.Fluent` namespace — `Schema()` / `Table()` / `Column()` and friends) has been removed. Declare schemas in DDL (`.sql`, the primary format) instead; to build a schema in code, implement `ISchemaProvider` directly and register it with `AddSchema<T>()`.
- **Breaking:** JSON schema support has been removed. The `AddJsonSchema` / `AddJsonSchemasFromGlob` / `AddJsonSchemasFromDirectory` helpers, the `JsonSchemaProvider`, and the built-in `json` schema serializer are gone — now that the SQL DDL is the primary format there is no reason to maintain a second one. Declare desired schemas in DDL (`.sql`) instead, and note that `Import` now writes `.sql` files.
- **Breaking:** With a single schema format, the format-selection machinery is gone: the `ISchemaSerializer` interface, `AddSchemaSerializer<T>(format)` / `UseSchemaSerializer<T>(format)`, the keyed serializer resolver, and `ImportArguments.Format` have all been removed. DDL is now read and written directly — NSchema DDL text is read by the public, stateless `DdlReader` (`DdlReader.Instance.Read(source)` returns a `DdlDocument` with the schema and any config blocks) and written by `DdlWriter` (`DdlWriter.Instance.Write(schema)`). The `DdlSchemaSerializer` and `DdlConfigReader` stream/string adapters were folded away.
- **Breaking:** `FileSchemaProvider` has been removed. It existed only to share file-loading across the JSON and DDL providers; with one format, that logic now lives directly in the internal `DdlSchemaProvider`. The `AddFileSchemasFromGlob` / `AddFileSchemasFromDirectory` primitives are gone too — use `AddSqlSchema` / `AddSqlSchemasFromGlob` / `AddSqlSchemasFromDirectory`, or implement `ISchemaProvider` directly.

### Fixed

- Toggling a column into or out of an identity column is now detected and emitted as a change. Previously only changes between two already-identity columns were picked up.
- Table privilege grants are now rendered by decomposing the privilege flags (e.g. `SELECT, INSERT`) instead of using the enum name, which could surface aliases like `ReadOnly` for `SELECT`.
- Re-importing into an existing schema file no longer fails with a duplicate-object error when the file already contains views (or the new enums/sequences); the merge now replaces them with the incoming definitions, as it always did for tables.
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
