# Extension points

Everything in the pipeline is registered through DI. You can replace defaults or add to the enumerable extension points.

| Interface                          | Purpose                                                                                      | Registered via                                                                                                     |
|------------------------------------|----------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------|
| `ISchemaProvider` (desired)        | Contribute schemas to the desired state. Usually via `AbstractSchemaProvider`.               | `AddSchema<T>()`                                                                                                   |
| `ISchemaProvider` (online current) | Read the current live database schema.                                                       | `UseCurrentSchema<T>()` (or via a provider package, e.g. `UsePostgres(...)`)                                       |
| `ISchemaPolicy`                    | Validate the merged desired schema.                                                          | `AddSchemaPolicy<T>()`                                                                                             |
| `IMigrationPlanTransformer`        | Rewrite or reorder the generated plan.                                                       | `AddPlanTransformer<T>()`                                                                                          |
| `IMigrationPolicy`                 | Validate the final plan before execution.                                                    | `AddMigrationPolicy<T>()`                                                                                          |
| `IScriptProvider`                  | Supply raw SQL to run pre- or post-deployment.                                               | `AddScriptProvider<T>()`, `AddScriptFromFile(...)`, `AddScriptsFromEmbeddedResources(...)`                         |
| `ISqlGenerator`                    | Generate the SQL for a migration plan, keyed by `Dialect`. Add support for another database. | `AddSqlGenerator<T>(dialect)` (or via a provider package, e.g. `UsePostgres(...)`); select with `WithDialect(...)` |
| `ISchemaDocumentSerializer`        | Read/write a desired-schema file format (JSON built-in), keyed by `Format`.                  | `AddSchemaSerializer<T>(format)`; `UseSchemaSerializer<T>(format)` to replace the built-in                         |
| `ISchemaImportTarget`              | Output destination for the `Import` operation, keyed by name.                                | `AddImportTarget<T>(name)` / `UseImportTarget<T>(name)` / `UseFileImportTarget(opts => ...)`                       |
| `ISchemaStateStore`                | Optional backend state store for tracking the last applied schema.                           | `UseStateStore<T>()` / `UseStateStore(instance)` / `UseFileStateStore(path)`                                       |

## Less commonly used extension points

These extension points are less commonly used, but still available for advanced scenarios.

| Interface                | Purpose                                                                                      | Registered via                                                   |
|--------------------------|----------------------------------------------------------------------------------------------|------------------------------------------------------------------|
| `IMigrationPlanner`      | Replace the planner that diffs the two schemas and produces a `MigrationPlanResult`.         | `AddSingleton<IMigrationPlanner, T>()`                           |
| `IDesiredSchemaProvider` | Replace how desired schemas are gathered and aggregated into a single `DatabaseSchema`.      | `AddSingleton<IDesiredSchemaProvider, T>()`                      |
| `ICurrentSchemaProvider` | Replace how online and offline current-state sources are selected and read                   | `AddSingleton<ICurrentSchemaProvider, T>()`                      |
| `IDiffRenderer`          | Customize how the migration diff is rendered to text (e.g. JSON instead of Terraform-style). | `UseTerraformRenderer(...)` / `AddSingleton<IDiffRenderer, T>()` |
| `IMigrationReporter`     | Customize run output. Register several and select one with `WithOutputFormat(...)`.          | `AddReporter<T>(format)` / `AddReporter(instance)`               |
| `ISchemaAggregator`      | Combine multiple desired schemas into a single schema for comparison.                        | `AddSingleton<ISchemaAggregator, T>()`                           |
| `ISchemaComparer`        | Compare the current and desired schemas to produce a migration plan.                         | `AddSingleton<ISchemaComparer, T>()`                             |
| `ISqlPlanRenderer`       | Customize how the SQL preview is rendered to text.                                           | `AddSingleton<ISqlPlanRenderer, T>()`                            |
| `ISqlExecutor`           | Override how SQL is sent to the database (e.g. logging, custom transactions).                | `UseSqlExecutor<T>()`                                            |

## Selecting one of many by key

A few seams let you register several implementations and pick one per run by a string key. All four are backed by `IKeyedResolver<TValue>`, which is injected directly into consumers.

| Seam                        | Key       | Registered via                   | Selected by                                               |
|-----------------------------|-----------|----------------------------------|-----------------------------------------------------------|
| `IMigrationReporter`        | `Format`  | `AddReporter<T>(format)`         | `WithOutputFormat(...)` / `OperationOptions.OutputFormat` |
| `ISqlGenerator`             | `Dialect` | `AddSqlGenerator<T>(dialect)`    | `WithDialect(...)` / `OperationOptions.Dialect`           |
| `ISchemaDocumentSerializer` | `Format`  | `AddSchemaSerializer<T>(format)` | the consumer (e.g. a file extension or CLI flag)          |
| `ISchemaImportTarget`       | name      | `AddImportTarget<T>(name)`       | `ImportOptions.Target` / auto-selected if exactly one     |
