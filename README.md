# ![NSchema](https://raw.githubusercontent.com/nschema-org/NSchema.Core/main/assets/nschema-logo-horizontal.png)

[![NSchema.Core](https://github.com/nschema-org/NSchema.Core/actions/workflows/cicd.yml/badge.svg)](https://github.com/nschema-org/NSchema.Core/actions/workflows/cicd.yml)

NSchema.Core is the engine behind the [NSchema CLI](https://github.com/nschema-org/NSchema), a declarative database schema migration tool. While this library is designed to be consumed directly, I'd recommend using the CLI tool, unless you have a specific reason to build your own harness around the Core package.

NSchema works by describing the schema you want using familiar SQL syntax. The library then compares it against the current state of your database, then runs the SQL to bring it in line.

Designed to be familiar to anyone who works with databases, with extensibility and safety features to support many different workflows.

## Getting started

Install the core package and a database provider:

```bash
dotnet add package NSchema.Core
dotnet add package NSchema.Postgres   # or another provider
```

Declare a schema in a `.sql` file using DDL. Write declarative `CREATE` statements describing the *desired* shape:

```sql
CREATE SCHEMA app;

CREATE TABLE app.users
(
    id bigint NOT NULL IDENTITY,
    email text NOT NULL,
    name text NOT NULL,
    CONSTRAINT users_pkey PRIMARY KEY (id),
    UNIQUE INDEX uc_users_email (email)
);
```

Wire up and run the application, loading the DDL files and a database provider:

```csharp
using NSchema;
using NSchema.Diff.Policies;
using NSchema.Postgres;

var builder = NSchemaApplication.CreateBuilder(args);

builder
    .AddSqlSchemas("schemas/**/*.sql")
    .UsePostgres(connectionString)
    .WithDestructiveActionPolicy(DestructiveActionPolicy.Warn);

var app = builder.Build();
await app.Apply();
```

On startup, NSchema introspects the database, compares it with your desired schema, and applies the resulting plan.

A run performs one of several operations; the common ones are:

- **`Plan`** (default) computes the plan and renders it without touching the database.
- **`Apply`** computes the plan and applies it.
- **`Refresh`** captures the current live schema to the state store without planning or applying.

Run an operation by calling the matching method on the built application — `app.Plan()` / `app.Apply()` / `app.Refresh()`. See [Configuration](docs/configuration.md#operations) for the full list.

You can also save a plan to a file and apply it later, unchanged so what was reviewed is exactly what runs:

```csharp
await app.Plan(new PlanArguments { OutFile = "migration.nplan" });
// ...review the saved plan, then later (e.g. in a separate CI step):
await app.Apply(new ApplyArguments { PlanFile = "migration.nplan" });
```

## Documentation

- **[Configuration](docs/configuration.md).** Building and running, operations, destructive-action policy, scoping, registering schemas, scripts, and policies.
- **[Defining schemas](docs/schemas.md).** Declaring schemas in DDL — a practical introduction.
- **[DDL grammar](docs/ddl-grammar.md).** The complete reference for the NSchema DDL: every statement, type, and the reserved configuration blocks.
- **[Concepts](docs/concepts.md).** The domain model, the pipeline, and how the pieces fit together.
- **[Extension points](docs/extension-points.md).** Every interface you can swap or extend, and how to register it.
- **[Samples](samples/).** Complete sample applications and reference implementations.

## License

See [LICENSE](LICENSE).
