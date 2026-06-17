# Defining schemas

The primary way to declare a schema is in a **SQL DDL file** using a declarative, dialect-agnostic schema language that borrows SQL's `CREATE TABLE` shape. You describe the *desired state* (the final shape of the schema), NSchema diffs it against the database and works out the changes.

This page is a practical introduction. The [grammar reference](ddl-grammar.md) is the complete specification.

You can also declare schemas in a [JSON file](#defining-schemas-in-json), which mirrors the domain model directly.

## A schema in DDL

```sql
--- The application schema.
CREATE SCHEMA app;

--- All registered users.
CREATE TABLE app.users
(
    id bigint NOT NULL IDENTITY,
    --- Primary contact; verified at signup.
    email varchar(255) NOT NULL,
    name text NOT NULL,
    role_id bigint NOT NULL,
    balance decimal(18, 2) DEFAULT (0),
    CONSTRAINT users_pkey PRIMARY KEY (id),
    CONSTRAINT fk_users_role_id FOREIGN KEY (role_id) REFERENCES app.roles (id) ON DELETE CASCADE,
    UNIQUE INDEX uc_users_email (email)
);

GRANT SELECT, INSERT ON app.users TO app_rw;
```

A few things to note, each covered in full by the [grammar reference](ddl-grammar.md):

- **It describes desired state, not migrations.** There is no `ALTER` — you write the final shape, and the planner derives the change. Type names (`bigint`, `varchar(255)`, `decimal(18,2)`) are canonical and dialect-agnostic; the `ISqlGenerator` maps them to the target database's spelling on output.
- **Constraints are always named** (`CONSTRAINT <name> …`) — the name is the comparer's match key, so changes diff stably. Indexes are written **inline** in the table body.
- **Comments are doc-comments.** A `---` line (or `/** … */` block) immediately above a declaration becomes that object's catalog comment (`COMMENT ON …`); a plain `--` comment is stripped.
- **Renames** use `RENAMED FROM <old>` on a schema, table, or column, so the comparer matches the existing object instead of dropping and recreating it.
- **Partial schemas** (`CREATE PARTIAL SCHEMA …`) leave undeclared tables alone rather than dropping them — useful for shared schemas. A `DROP TABLE app.x;` statement records an explicit drop.
- **Other objects** — views (`CREATE VIEW`), enums (`CREATE ENUM`), sequences (`CREATE SEQUENCE`), and functions/procedures (`CREATE FUNCTION` / `CREATE PROCEDURE`) — are declared with their own statements. See the grammar reference.

## Registering DDL files

Load `.sql` files with `AddSqlSchemasFromGlob` (or `AddSqlSchema(path)` for a single file, `AddSqlSchemasFromDirectory(dir)` for a directory):

```csharp
builder.AddSqlSchemasFromGlob("schemas/**/*.sql");
```

To bootstrap a project from an existing database, use the [`Import` operation](configuration.md#operations), which writes the live schema out as DDL source files.

### SQL types

SQL types are written as compact strings rather than objects. Parameterless types are just their name; sized and precision types include their arguments:

| String                                                       | Equivalent                                                  |
|--------------------------------------------------------------|-------------------------------------------------------------|
| `boolean`, `tinyint`, `smallint`, `int`, `bigint`            | `SqlType.Boolean` ... `SqlType.BigInt`                      |
| `float`, `double`                                            | `SqlType.Float`, `SqlType.Double`                           |
| `text`, `date`, `time`, `datetime`, `datetimeoffset`, `guid` | `SqlType.Text` ... `SqlType.Guid`                           |
| `decimal(18,2)`                                              | `SqlType.Decimal(18, 2)`                                    |
| `char(8)`, `nchar(4)`, `binary(16)`                          | `SqlType.Char(8)`, `SqlType.NChar(4)`, `SqlType.Binary(16)` |
| `varchar`, `varchar(255)`                                    | `SqlType.VarChar()`, `SqlType.VarChar(255)`                 |
| `nvarchar`, `nvarchar(64)`                                   | `SqlType.NVarChar()`, `SqlType.NVarChar(64)`                |
| `varbinary`, `varbinary(32)`                                 | `SqlType.VarBinary()`, `SqlType.VarBinary(32)`              |
| any other value, e.g. `jsonb`                                | `SqlType.Custom("jsonb")`                                   |

Any string that isn't a recognized built-in type becomes a custom type, which is how you target database-specific types like `jsonb` or a schema-qualified enum (`app.status`).
