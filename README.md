# ![NSchema](https://raw.githubusercontent.com/tom-wolfe/NSchema/main/assets/nschema-logo-horizontal.png)

NSchema is a declarative database schema migration library for .NET.

You describe the schema you want in C#. NSchema compares it against the current state of your database, then runs the SQL to bring it in line.

Designed to be familiar to .NET devs, with extensibility and safety features to support many different workflows.

## Getting started

Install the core package and a database provider:

```bash
dotnet add package NSchema
dotnet add package NSchema.Postgres   # or another provider
```

Declare a schema by subclassing `AbstractSchemaProvider`:

```csharp
using NSchema.Schema;
using NSchema.Schema.Fluent;

public class AppSchema : AbstractSchemaProvider
{
    public AppSchema()
    {
        Schema("app", s => s
            .Table("users", t => t
                .Column("id", SqlType.Text, c => c.PrimaryKey("users_pkey"))
                .Column("email", SqlType.Text, c => c.NotNull())
                .Column("name", SqlType.Text, c => c.NotNull())
                .Index("uc_users_email", ["email"], i => i.Unique())
            )
        );
    }
}
```

Wire up and run the application:

```csharp
using NSchema;
using NSchema.Migration;
using NSchema.Postgres;

var builder = NSchemaApplication.CreateBuilder(args);

builder
    .AddSchemasFromAssemblyContaining<Program>()
    .UsePostgres(connectionString)
    .WithDestructiveActionPolicy(DestructiveActionPolicy.Warn);

var app = builder.Build();
await app.Apply();
```

On startup, NSchema introspects the database, compares it with your desired schema, and applies the resulting plan.

A run performs one of three operations:

- **`Plan`** (default) computes the plan and renders it without touching the database.
- **`Apply`** computes the plan and applies it.
- **`Refresh`** captures the current live schema to the state store without planning or applying.

Call `app.Plan()` / `app.Apply()` / `app.Refresh()` explicitly, or configure the default via `RunOperation(...)` and use `RunAsync()`. See [Configuration](docs/configuration.md#operations) for details.

## Documentation

- **[Configuration](docs/configuration.md).** Hosting, operations, destructive-action policy, scoping, registering schemas, scripts, and policies.
- **[Defining schemas](docs/schemas.md).** The full fluent reference for schemas, tables, columns, foreign keys, and indexes.
- **[Concepts](docs/concepts.md).** The domain model, the pipeline, and how the pieces fit together.
- **[Extension points](docs/extension-points.md).** Every interface you can swap or extend, and how to register it.
- **[Samples](samples/).** Complete sample applications and reference implementations.
- **[Roadmap](docs/roadmap.md).** Planned features and improvements.

## License

See [LICENSE](LICENSE).
