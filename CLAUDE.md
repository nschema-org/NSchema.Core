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

`DefaultMigrationPlanner` (`src/NSchema/Migration/DefaultMigrationPlanner.cs`) is a pure domain service. It takes two pre-resolved `DatabaseSchema` values and produces a `MigrationPlanResult`:

1. **Validate desired** — every `ISchemaPolicy` runs against the desired schema.
2. **Diff** — `ISchemaComparer` produces the initial `MigrationPlan`.
3. **Inject scripts** — pre/post scripts from `IScriptProvider`s are inserted.
4. **Transform** — `IMigrationPlanTransformer`s run in registration order. The built-in `ActionOrderingTransformer` sorts actions into a safe dependency order.
5. **Validate plan** — every `IMigrationPolicy` runs against the transformed plan. The built-in `DestructiveActionMigrationPolicy` enforces `MigrationOptions.DestructiveActionPolicy`.

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

| Interface | Registered via |
|-----------|----------------|
| `ISchemaProvider` (desired) | `AddSchema<T>()` / `AddSchemasFromAssembly[Containing]<T>()` |
| `ISchemaProvider` (online current) | `UseCurrentSchema<T>()` — typically called from a database-provider extension (e.g. `UsePostgres(...)`) |
| `IMigrationOperation` | `AddKeyedSingleton<IMigrationOperation, T>(MigrationOperation.*)` |
| `ISchemaPolicy` | `AddSchemaPolicy<T>()` |
| `IMigrationPlanTransformer` | `AddPlanTransformer<T>()` |
| `IMigrationPolicy` | `AddMigrationPolicy<T>()` |
| `IScriptProvider` | `AddScriptProvider<T>()` / `AddScriptFromFile(...)` / `AddScriptsFromEmbeddedResources(...)` |
| `ISqlExecutor` | `UseSqlExecutor<T>()` (replaces default) |
| `IMigrationCompiler` | `UseMigrationCompiler<T>()` (replaces default `SqlMigrationCompiler`) |
| `ISchemaStateStore` | `UseStateStore<T>()` / `UseStateStore(instance)` / `UseStateStoreFile(path)` |
| `ISchemaComparer`, `ISchemaAggregator`, `IMigrationPlanner`, `IMigrationReporter`, `IMigrationPlanRenderer` | Override via `Services.AddSingleton<...>()` before `Build()` (defaults registered with `TryAdd`) |
| `ISqlPlanner` | Supplied by a database-provider extension |

### Renaming

Schemas, tables, and columns support rename detection via the fluent `RenamedFrom(oldName)` method, which sets the `OldName` property on the domain model. The comparer matches on `OldName` when the current name isn't found.

### Configuration options

**`MigrationOptions`** — what to migrate:
- `DestructiveActionPolicy` — `Error` (default), `Warn`, or `Allow`. Enforced by `DestructiveActionMigrationPolicy`. Configured via `WithDestructiveActionPolicy(...)`.
- `SchemaNames` — optional `string[]` scope filter. When set, only these schemas are read, validated, and diffed. When unset, scope is derived from declared and dropped schemas. Configured via `ForSchemas(...)`.

**`MigrationRunOptions`** — how to run it:
- `Operation` — `Plan` (default), `Apply`, or `Refresh`. Configured via `RunOperation(...)`, or overridden per-run by calling `NSchemaApplication.Plan()` / `Apply()` / `Refresh()`.
- `TransactionMode` — `Single` (default; whole plan in one transaction, with carve-outs for statements marked `RunOutsideTransaction`) or `None`.
