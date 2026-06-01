# Extension points

Everything in the pipeline is registered through DI. You can replace defaults or add to the enumerable extension points.

| Interface                          | Purpose                                                                                                 | Registered via                                                                             |
|------------------------------------|---------------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------|
| `ISchemaProvider` (desired)        | Contribute schemas to the desired state. Usually via `AbstractSchemaProvider`.                          | `AddSchema<T>()`                                                                           |
| `ISchemaProvider` (online current) | Read the current live database schema.                                                                  | `UseCurrentSchema<T>()` (or via a provider package, e.g. `UsePostgres(...)`)               |
| `ISchemaPolicy`                    | Validate the merged desired schema.                                                                     | `AddSchemaPolicy<T>()`                                                                     |
| `IMigrationPlanTransformer`        | Rewrite or reorder the generated plan.                                                                  | `AddPlanTransformer<T>()`                                                                  |
| `IMigrationPolicy`                 | Validate the final plan before execution.                                                               | `AddMigrationPolicy<T>()`                                                                  |
| `IScriptProvider`                  | Supply raw SQL to run pre- or post-deployment.                                                          | `AddScriptProvider<T>()`, `AddScriptFromFile(...)`, `AddScriptsFromEmbeddedResources(...)` |
| `ISqlExecutor`                     | Override how SQL is sent to the database (e.g. logging, custom transactions).                           | `UseSqlExecutor<T>()`                                                                      |
| `IMigrationCompiler`               | Replace how a plan is compiled into an executable unit (e.g. emit SQL to a file instead of running it). | `UseMigrationCompiler<T>()`                                                                |
| `ISqlPlanner`                      | Add support for another database.                                                                       | `UseSqlPlanner<T>()` (or via a provider package, e.g. `UsePostgres(...)`)                  |
| `ISchemaStateStore`                | Optional backend state store for tracking the last applied schema.                                      | `UseStateStore<T>()` / `UseStateStore(instance)` / `UseFileStateStore(path)`               |

## Less commonly used extension points

These extension points are less commonly used, but still available for advanced scenarios.

| Interface                | Purpose                                                                                 | Registered via                                                    |
|--------------------------|-----------------------------------------------------------------------------------------|-------------------------------------------------------------------|
| `IMigrationPlanner`      | Replace the planner that diffs the two schemas and produces a `MigrationPlanResult`.    | `AddSingleton<IMigrationPlanner, T>()`                            |
| `IDesiredSchemaProvider` | Replace how desired schemas are gathered and aggregated into a single `DatabaseSchema`. | `AddSingleton<IDesiredSchemaProvider, T>()`                       |
| `ICurrentSchemaProvider` | Replace how online and offline current-state sources are selected and read              | `AddSingleton<ICurrentSchemaProvider, T>()`                       |
| `IMigrationPlanRenderer` | Customize how the migration plan is converted to a string.                              | `AddSingleton<IMigrationPlanRenderer, T>()`                       |
| `IMigrationReporter`     | Customize how user updates are reported to the terminal and logger.                     | `AddSingleton<IMigrationReporter, T>()`                           |
| `ISchemaAggregator`      | Combine multiple desired schemas into a single schema for comparison.                   | `AddSingleton<ISchemaAggregator, T>()`                            |
| `ISchemaComparer`        | Compare the current and desired schemas to produce a migration plan.                    | `AddSingleton<ISchemaComparer, T>()`                              |
| `IMigrationOperation`    | Implement a custom operation (plan, apply, refresh, or a new one).                      | `AddKeyedSingleton<IMigrationOperation, T>(MigrationOperation.*)` |
