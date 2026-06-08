# Configuration

How to build, run, and configure an NSchema application. New to NSchema? Start with the [README](../README.md) quickstart, then come back here.

## Building and running

The builder should be familiar to any developer who's used ASP.NET. `NSchemaApplication.CreateBuilder(...)` produces a builder that you can configure with your target schema, database provider, configuration, logging, metrics, or any other .NET packages that we've come to rely on — it uses a `HostApplicationBuilder` internally purely to compose those.

Call `builder.Build()` to get an `NSchemaApplication`. It is not a long-running host; it's a single-use object that runs one operation and exits. Run an operation by calling the matching method on it (`Plan()`, `Apply()`, etc.). The operation `await`s directly, so exceptions surface to you at the call site, and the application can only be run once.

## Operations

Each run performs one of the following operations, selected by the method you call on the built application:

- **`Plan()`** computes and renders the plan, without touching the database.
- **`Apply()`** computes the plan and applies it to the database. After a successful apply, the resulting schema is captured to the [state store](#backend-state-store) if one is configured.
- **`Refresh()`** reads the current schema from the live database and writes it to the state store, without planning or applying anything. Requires a state store.
- **`Import()`** reads the live database schema and writes it to the local filesystem as desired-schema source files (destination, partitioning, and format are set per run via `ImportArguments`). Useful for bootstrapping a project from an existing database.
- **`Validate()`** loads the desired schema and validates it against the configured schema policies, without planning or applying.
- **`Destroy()`** drops the managed schema objects from the database.

```csharp
var app = builder.Build();
await app.Apply(); // Runs the apply.
```

## Backend state store

By default, NSchema generates plans against the current live state of the database. This is simple and works well for many scenarios, but you can't always guarantee that you'll have access to the database at plan time. Sometimes it's desirable to generate a plan against the last applied state instead, like generating migration scripts in a CI pipeline with no database connection.

NSchema supports an optional backend state store that persists a snapshot of the schema. After a successful apply, NSchema captures the resulting schema to the store, so a later plan can be generated against that snapshot. You can also capture the current schema without applying by running a [`Refresh`](#operations) operation — handy for recording drift that happened between applies.

Register a state store from a provider package like `NSchema.Aws`, or use the built-in `UseFileStateStore(path)` for a file-backed store:

```csharp
builder.UseFileStateStore("schema_state.json");
```

When a state store is registered, `Plan` operations automatically use it as the current-state source (offline planning), and `Apply` operations always read from the live database. No additional configuration is needed.

## Destructive action policy

By default, NSchema will error on any destructive actions (e.g. dropping tables or columns) to prevent accidental data loss. You can change this behavior by configuring the `DestructiveActionPolicy`:

```csharp
builder.WithDestructiveActionPolicy(DestructiveActionPolicy.Warn); // log a warning, but continue with the migration
builder.WithDestructiveActionPolicy(DestructiveActionPolicy.Allow); // allow destructive actions without warning
```

If you need more advanced control, you can implement your own `IPlanPolicy` and register it with `AddPlanPolicy<T>()`.

## Scoping to specific schemas

Pass a `Schemas` filter on the operation arguments to scope a run to a subset of schemas. Useful for deploying schemas independently of one another:

```csharp
var app = builder
    .AddSchemasFromAssemblyContaining<Program>()
    .UsePostgres(connectionString)
    .Build();

await app.Plan(new PlanArguments { Schemas = ["app"] });   // only "app" is read, validated, and diffed
```

Scope is a per-invocation argument (`PlanArguments` / `ApplyArguments` / `ValidateArguments` / `DestroyArguments`), not ambient configuration. Declarations or drops for schemas outside the scope are ignored, so unmanaged schemas in the database are never touched.

## Configuring desired schemas

The desired schema(s) are configured by registering one or more `ISchemaProvider` implementations that can supply the target schema. The most common way to do this is to subclass `AbstractSchemaProvider`, but you can also implement `ISchemaProvider` directly if you need more control.

```csharp
builder
    .AddSchema<AppSchema>() // register a single schema provider
    .AddSchemasFromAssemblyContaining<AppSchema>(); // register all providers in the assembly
```

Schemas can also be loaded from a JSON file instead of C#, with no extra package required:

```csharp
builder.AddJsonSchema("schema.json");
```

See [Defining schemas](schemas.md) for the full declaration reference, including the [JSON format](schemas.md#defining-schemas-in-json).

## Configuring the current schema

The current schema is configured by registering a provider that can read the live state of the database. This is typically done via a provider package like `NSchema.Postgres`, which includes an implementation that reads directly from the database.

```csharp
// Using a provider package:
builder.UsePostgres(connectionString);

// Or directly:
builder.UseCurrentSchema<PostgresSchemaProvider>();
```

See [Backend state store](#backend-state-store) for how to configure offline planning against a persisted snapshot.

## Adding scripts

You can add pre- or post-deployment scripts to run alongside the generated migration SQL. This is useful for data migrations, cache invalidation, or any other custom logic that needs to run as part of the deployment.

Scripts can also be added from embedded resources, or by implementing `IScriptProvider` directly for more complex scenarios:

```csharp
// Add scripts from files:
builder
    .AddScriptFromFile(ScriptType.PreDeployment, "pre_deploy.sql")
    .AddScriptFromFile(ScriptType.PostDeployment, "post_deploy.sql");

// Add scripts from embedded resources:
builder.AddScriptsFromEmbeddedResources(ScriptType.PreDeployment, typeof(Program).Assembly, "MyNamespace.Scripts.PreDeployment.");

// Add a custom script provider:
builder.AddScriptProvider<CustomScriptProvider>();
```

## Transaction mode

By default, NSchema runs the entire migration inside a single transaction to ensure that either all changes are applied successfully or none at all. However, some databases don't support DDL statements inside transactions, or you may have specific statements that need to run outside of a transaction.

You can configure the transaction mode with `WithTransactionMode(...)`:

```csharp
builder.WithTransactionMode(TransactionMode.Single); // run the entire migration in a single transaction (default)
builder.WithTransactionMode(TransactionMode.None); // run all statements outside of transactions
```

## Output format

Run output is produced by an `IOperationReporter`. The built-in `default` reporter writes human-readable output to the terminal. Register additional reporters by format key on the builder, and select which one a run uses via `NSchemaApplicationOptions.Reporter`:

```csharp
var app = NSchemaApplication
    .CreateBuilder(new NSchemaApplicationOptions { Reporter = "json" })  // select the reporter for this run
    .AddReporter<JsonReporter>("json")                                   // register it under that key
    .Build();
```

## SQL dialect

When more than one `ISqlGenerator` is registered (each declaring a `Dialect`), choose which one generates the SQL for a run:

```csharp
builder
    .AddSqlGenerator<PostgresGenerator>("postgres")
    .AddSqlGenerator<MySqlGenerator>("mysql")
    .WithDialect("postgres");    // or set SqlOptions.Dialect
```

## Schema policies

Schema policies are used to validate the desired schema before any comparison or planning is done:

```csharp
builder.AddSchemaPolicy<TableNamesMustBePluralPolicy>();
```

## Plan policies

Plan policies are used to validate the generated migration plan before it's executed:

```csharp
builder.AddPlanPolicy<NoDestructiveActionsPolicy>();
```

## Plan transformers

Plan transformers are used to rewrite or reorder the generated migration plan before it's validated and executed:

```csharp
builder.AddPlanTransformer<DependencyOrderingPlanTransformer>();
```

## Exception behavior

By default, NSchema will report any exceptions using `IOperationReporter` and then rethrow them. You can change this behavior through `NSchemaApplicationOptions`, passed when you create the builder:

```csharp
// report and rethrow exceptions (default)
var app = NSchemaApplication.CreateBuilder(new NSchemaApplicationOptions
{
    ExceptionBehavior = ExceptionBehavior.ReportAndThrow,
});

// rethrow exceptions without reporting
var app = NSchemaApplication.CreateBuilder(new NSchemaApplicationOptions
{
    ExceptionBehavior = ExceptionBehavior.Throw,
});
```

Either way the exception propagates out of the operation call (e.g. `await app.Apply()`), so you can handle it and set a process exit code in your entry point.
