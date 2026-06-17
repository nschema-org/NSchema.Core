# Extension points

Everything in the pipeline is registered through DI. You can replace defaults or add to the enumerable extension points.

| Interface                          | Purpose                                                                                      | Registered via                                                                                                     |
|------------------------------------|----------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------|
| SQL DDL files (desired)            | The desired schema and any inline `PRE/POST DEPLOYMENT` scripts.                             | `AddSqlSchemas(...)` (may be called more than once; sources aggregate)                                             |
| `ISchemaProvider` (online current) | Read the current live database schema.                                                       | `UseCurrentSchema<T>()` (or via a provider package, e.g. `UsePostgres(...)`)                                       |
| `ISchemaPolicy`                    | Validate the merged desired schema.                                                          | `AddSchemaPolicy<T>()`                                                                                             |
| `ISqlGenerator`                    | Generate the SQL for a migration plan, keyed by `Dialect`. Add support for another database. | `AddSqlGenerator<T>(dialect)` (or via a provider package, e.g. `UsePostgres(...)`); select with `WithDialect(...)` |
| `ISchemaStateStore`                | Optional backend state store for tracking the last applied schema.                           | `UseStateStore<T>()` / `UseStateStore(instance)` / `UseFileStateStore(path)`                                       |

## Less commonly used extension points

These extension points are less commonly used, but still available for advanced scenarios.

| Interface            | Purpose                                                                                         | Registered via                                                         |
|----------------------|-------------------------------------------------------------------------------------------------|------------------------------------------------------------------------|
| `IDiffRenderer`      | Customize how the migration diff is rendered to text (e.g. JSON instead of Terraform-style).    | `UseTerraformRenderer(...)` / `UseDiffRenderer<TRenderer>()`           |
| `ISqlPlanRenderer`   | Customize how the SQL preview is rendered to text (e.g. JSON for CI).                           | `UseSqlPlanRenderer<TRenderer>()`                                      |
| `IOperationReporter` | Customize run output. Register several and select one via `NSchemaApplicationOptions.Reporter`. | `AddReporter<T>(format)` / `AddReporter(instance)` (last-wins per key) |
| `ISqlExecutor`       | Override how SQL is sent to the database (e.g. logging, custom transactions).                   | `UseSqlExecutor<T>()`                                                  |
