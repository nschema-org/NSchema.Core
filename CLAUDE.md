# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build

# Run all tests
dotnet test

# Run a single test class
dotnet test tests/NSchema.Tests --filter "FullyQualifiedName~DefaultSchemaComparerTests"
```

Note: the Postgres provider was extracted to its own repository (see commit `dd3e695`). The solution (`NSchema.slnx`) contains only `src/NSchema` and `tests/NSchema.Tests`, where all real code lives.

## Architecture

NSchema is a declarative database schema migration library for .NET. The user describes the schema they want via `AbstractSchemaProvider`; NSchema introspects the database, diffs, and applies the difference.

The application is a hosted .NET app. `NSchemaApplication.CreateBuilder()` returns an `NSchemaApplicationBuilder` (wraps `HostApplicationBuilder`). `Build()` produces an `NSchemaApplication` (`IHost`). The migration runs as a `BackgroundService` — `NSchemaHost` (`src/NSchema/Hosting/NSchemaHost.cs`) — which resolves the configured `IMigrationOperation` by key from DI, calls `Execute()`, then signals `IHostApplicationLifetime` to stop.

### Layer separation

The codebase is split into two layers:

- **`Migration/` — domain layer.** Pure planning logic: `IMigrationPlanner`, `ISchemaComparer`, `ISchemaAggregator`, schema policies, migration policies, transformers, the schema model. No knowledge of operations or how a run is orchestrated.
- **`Hosting/` — application layer.** Orchestration: `NSchemaHost`, `IMigrationOperation` implementations, `IStateCapturer`. Knows about operations, schema resolution, and state capture.

### Operations

`NSchemaHost` reads `MigrationRunOptions.Operation`, resolves `GetRequiredKeyedService<IMigrationOperation>(operation)`, and calls `Execute()`. Adding a new operation requires a new class and a new keyed registration — no changes to `NSchemaHost`.

Built-in operations (`src/NSchema/Hosting/Operations/`):

- **`PlanOperation`** — resolves schemas (offline source preferred), calls `IMigrationPlanner`, reports the plan and preview (if a compiler is registered). Does not execute.
- **`ApplyOperation`** — resolves schemas (online source required), calls `IMigrationPlanner`, compiles, previews, executes, and captures state.
- **`RefreshOperation`** — captures the live schema to the state store without planning or applying.

`SchemaResolution.ResolveAsync` (`src/NSchema/Hosting/Operations/SchemaResolution.cs`) is a shared static helper used by `PlanOperation` and `ApplyOperation` to collect desired providers, aggregate, derive scope, and fetch the current schema.

### Schema providers

`ISchemaProvider` (`src/NSchema/Migration/ISchemaProvider.cs`) is used for **desired-state** providers only:

```csharp
Task<DatabaseSchema> GetSchema(string[]? schemaNames = null, CancellationToken cancellationToken = default);
```

Desired providers are registered as an enumerable `ISchemaProvider` via `AddSchema<T>()` or assembly scanning. There can be many; they are aggregated by `ISchemaAggregator` before planning.

**Current-state** schema access goes through `ICurrentSchemaProvider` (`src/NSchema/Migration/ICurrentSchemaProvider.cs`):

```csharp
ISchemaProvider GetSource(SchemaSourceMode preferred, bool required = true);
```

- `GetSource(Online)` — returns the live database provider (registered via `UseCurrentSchema<T>()`). Throws if not configured.
- `GetSource(Offline)` — returns the state-backed provider (requires a registered `ISchemaStateStore`). Throws if not configured.
- `GetSource(Offline, required: false)` — prefers offline, falls back to online, throws only if neither exists.

The internal `DefaultCurrentSchemaProvider` wires both sources together. Registering a state store via `UseStateStore*()` is all that's needed to enable offline planning.

### Planner

`DefaultMigrationPlanner` (`src/NSchema/Migration/DefaultMigrationPlanner.cs`) is a pure domain service. It takes two pre-resolved `DatabaseSchema` values and produces a `MigrationPlanResult` (which carries both the executable `MigrationPlan` and its structured `MigrationDiff`). The **structured diff is the primary artifact**: the comparer emits it directly, and the linearizer derives the ordered plan from it. The pipeline is three stages, each of which transforms its representation and then validates it:

1. **Schema stage** — `ISchemaTransformer`s run in registration order against the desired schema, then every `ISchemaPolicy` validates it. A schema-policy error is fatal and skips the rest.
2. **Diff stage** — `ISchemaComparer` produces the structured `MigrationDiff` directly (no flat-action intermediate). Deployment-script names from `IScriptProvider`s are folded onto the diff for rendering. `IDiffTransformer`s then run in registration order, and every `IDiffPolicy` validates the result. The built-in `DestructiveActionMigrationPolicy` runs here (it reasons over `ChangeKind.Remove` and narrowing column changes) and enforces `MigrationOptions.DestructiveActionPolicy`.
3. **Plan stage** — `IMigrationLinearizer` (default `DefaultMigrationLinearizer`) walks the diff and emits the migration actions, ordering them into a safe dependency order via `ActionOrderingTransformer`. The collected scripts are spliced in as `RunScript` actions (pre first, post last). `IMigrationPlanTransformer`s then run in registration order, and every `IMigrationPolicy` validates the final plan.

The planner has no knowledge of operations, online/offline routing, or `MigrationRunOptions`.

### Compilation and execution

`IMigrationCompiler` (default `SqlMigrationCompiler`) turns a `MigrationPlan` into an `ICompiledMigration`: an inspectable unit with a `Preview` (SQL statements) and an `Execute`. The compiler is optional — an offline run with no database provider registers none, and `PlanOperation` reports the plan without a SQL preview.

`DefaultSqlExecutor` executes the `SqlPlan` using a `DbDataSource`. It reads `MigrationRunOptions.TransactionMode` to decide whether to wrap everything in one transaction.

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

| Interface                                                                                                   | Registered via                                                                                          |
|-------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------|
| `ISchemaProvider` (desired)                                                                                 | `AddSchema<T>()` / `AddSchemasFromAssembly[Containing]<T>()`                                            |
| `ISchemaProvider` (online current)                                                                          | `UseCurrentSchema<T>()` — typically called from a database-provider extension (e.g. `UsePostgres(...)`) |
| `IMigrationOperation`                                                                                       | `AddKeyedSingleton<IMigrationOperation, T>(MigrationOperation.*)`                                       |
| `ISchemaTransformer`                                                                                        | `AddSchemaTransformer<T>()` / `AddSchemaTransformersFromAssembly[Containing]<T>()`                      |
| `ISchemaPolicy`                                                                                             | `AddSchemaPolicy<T>()`                                                                                  |
| `IDiffTransformer`                                                                                          | `AddDiffTransformer<T>()` / `AddDiffTransformersFromAssembly[Containing]<T>()`                          |
| `IDiffPolicy`                                                                                               | `AddDiffPolicy<T>()` / `AddDiffPoliciesFromAssembly[Containing]<T>()`                                   |
| `IMigrationPlanTransformer`                                                                                 | `AddPlanTransformer<T>()`                                                                               |
| `IMigrationPolicy`                                                                                          | `AddMigrationPolicy<T>()`                                                                               |
| `IScriptProvider`                                                                                           | `AddScriptProvider<T>()` / `AddScriptFromFile(...)` / `AddScriptsFromEmbeddedResources(...)`            |
| `ISqlExecutor`                                                                                              | `UseSqlExecutor<T>()` (replaces default)                                                                |
| `IMigrationCompiler`                                                                                        | `UseMigrationCompiler<T>()` (replaces default `SqlMigrationCompiler`)                                   |
| `ISchemaStateStore`                                                                                         | `UseStateStore<T>()` / `UseStateStore(instance)` / `UseFileStateStore(path)`                            |
| `ISchemaComparer`, `IMigrationLinearizer`, `ISchemaAggregator`, `IMigrationPlanner`, `IMigrationReporter`   | Override via `Services.AddSingleton<...>()` before `Build()` (defaults registered with `TryAdd`)        |
| `IDiffRenderer`                                                                                             | `UseTerraformRenderer(...)`, or override via `Services.AddSingleton<...>()` before `Build()`            |
| `ISqlPlanner`                                                                                               | Supplied by a database-provider extension                                                               |

### Diff rendering

The structured `MigrationDiff` (`Migration/Diff/Model/`: schema → table → columns/indexes/constraints/grants, each carrying a `ChangeKind` of `Add`/`Modify`/`Remove`) is produced directly by `ISchemaComparer` during planning and carried on `MigrationPlanResult.Diff`. The model is presentation-agnostic, and it is the semantic source of truth — `IMigrationLinearizer` derives the executable plan from it, so a diff transformer that changes the tree also changes what executes.

Rendering is a single phase: **`IDiffRenderer`** (default `TerraformDiffRenderer`) turns a `MigrationDiff` into text. The default emits a Terraform-style diff; an alternative (e.g. JSON) can be registered without touching the diff projection. Each renderer owns its own options POCO — the Terraform renderer reads `TerraformDiffRendererOptions.IncludeColour` (defaulted from the environment via `EnvironmentHelpers.SupportsColor`: on unless `NO_COLOR` is set or output is redirected), configured through `UseTerraformRenderer(o => ...)`. The renderer itself never reads the environment.

`IMigrationReporter.ReportDiff(MigrationDiff)` is the seam: the reporter owns the `IDiffRenderer` and writes the rendered text to its output. `MigrationHelper.Prepare` simply hands `result.Diff` to the reporter — there is no separate diff-building step.

### Renaming

Schemas, tables, and columns support rename detection via the fluent `RenamedFrom(oldName)` method, which sets the `OldName` property on the domain model. The comparer matches on `OldName` when the current name isn't found.

### Configuration options

**`MigrationOptions`** — what to migrate:
- `DestructiveActionPolicy` — `Error` (default), `Warn`, or `Allow`. Enforced by `DestructiveActionMigrationPolicy`. Configured via `WithDestructiveActionPolicy(...)`.
- `SchemaNames` — optional `string[]` scope filter. When set, only these schemas are read, validated, and diffed. When unset, scope is derived from declared and dropped schemas. Configured via `ForSchemas(...)`.

**`MigrationRunOptions`** — how to run it:
- `Operation` — `Plan` (default), `Apply`, or `Refresh`. Configured via `RunOperation(...)`, or overridden per-run by calling `NSchemaApplication.Plan()` / `Apply()` / `Refresh()`.
- `TransactionMode` — `Single` (default; whole plan in one transaction, with carve-outs for statements marked `RunOutsideTransaction`) or `None`.
