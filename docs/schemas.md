# Defining schemas

The easiest way to declare a schema is to subclass `AbstractSchemaProvider`, as in the [README](../README.md) quickstart. This page is the full reference for the fluent declaration API.

You can also declare schemas in a [JSON file](#defining-schemas-in-json) instead of C#, which is useful when you don't want a compiled project just to describe a schema.

## Schema declaration

Schemas are declared as follows:

```csharp
using NSchema.Schema;
using NSchema.Schema.Fluent;

public class AppSchema : AbstractSchemaProvider
{
    public AppSchema()
    {
        Schema("app", s =>
        {
            // Configure schema here.
            s.Comment("This is the app schema.");
            s.RenamedFrom("old_app");
            s.AsPartial();
        });

        Schema("old_schema", s => s.Dropped());
    }
}
```

- `Name` declares the name of the schema in the database.
- `Table(...)` declares a table within the schema.
- `Grant(...)` grants a role usage on the schema.
- `Comment(...)` adds a comment to the schema.
- `RenamedFrom(...)` marks the schema as renamed from an existing one, so the comparer can match it instead of dropping and recreating.
- `AsPartial()` marks the schema as partial, meaning that tables not declared here won't be dropped. This is useful for shared schemas, or when you want to manage some tables manually.
- `Dropped()` marks the schema as dropped, meaning it will be dropped if it exists.

## Table declaration

Tables are declared within a schema:

```csharp
public class AppSchema : AbstractSchemaProvider
{
    public AppSchema()
    {
        Schema("app", s =>
        {
            s.Table("users", t => {
                // Configure table here.
                t.Column("id", SqlType.Text, c => c.PrimaryKey("users_pkey"));
                t.Column("email", SqlType.Text, c => c.NotNull());
                t.Column("name", SqlType.Text, c => c.NotNull());
                t.Index("uc_users_email", ["email"], i => i.Unique());

                t.Comment("This is the users table.");
                t.RenamedFrom("old_users");
            });
        });
    }
}
```

- `Name` declares the name of the table in the database.
- `Column(...)` declares a column with the table.
- `PrimaryKey(...)` declares a primary key constraint on the table.
- `ForeignKey(...)` declares a foreign key constraint on the table.
- `Index(...)` declares an index on the table.
- `Grant(...)` grants a role SELECT, INSERT, UPDATE or DELETE on the table.
- `Comment(...)` adds a comment to the table.
- `RenamedFrom(...)` marks the table as renamed from an existing one, so the comparer can match it instead of dropping and recreating.
- `Dropped()` marks the table as dropped, meaning it will be dropped if it exists. Only necessary when dropping a table from a partial schema, otherwise the comparer will detect it as missing and drop it automatically.

## Column declaration

```csharp
public class AppSchema : AbstractSchemaProvider
{
    public AppSchema()
    {
        Schema("app", s =>
        {
            s.Table("users", t => {
                t.Column("id", SqlType.Text, c => {
                    c.NotNull();
                    c.PrimaryKey("users_pkey");
                });
            });
        });
    }
}
```

- `Name` declares the name of the column in the database.
- `SqlType` declares the SQL type of the column. This is an abstract type that the database provider will map to a concrete type.
- `NotNull()` marks the column as not nullable.
- `Nullable()` marks the column as nullable.
- `Default(...)` declares a default value for the column.
- `Comment(...)` adds a comment to the column.
- `RenamedFrom(...)` marks the column as renamed from an existing one, so the comparer can match it instead of dropping and recreating.
- `Identity(...)` marks the column as an identity/auto-increment column, with optional configuration for seed and increment values.
- `PrimaryKey(...)` declares a primary key constraint on the column.

## Foreign key declaration

```csharp
public class AppSchema : AbstractSchemaProvider
{
    public AppSchema()
    {
        Schema("app", s =>
        {
            s.Table("users", t => {
                t.ForeignKey("FK_users_role_id", ["role_id"], "app", "roles", ["id"], fk =>
                {
                    fk.OnUpdate(ReferentialAction.SetDefault);
                    fk.OnDelete(ReferentialAction.Cascade);
                });
            });
        });
    }
}
```

- `Name` declares the name of the foreign key constraint in the database.
- `ColumnNames` declares the columns in the source table that are part of the foreign key.
- `ReferencedSchema` declares the schema of the referenced table.
- `ReferencedTable` declares the name of the referenced table.
- `ReferencedColumns` declares the columns in the referenced table that are part of the foreign key.
- `OnUpdate(...)` declares the referential action to take on updates (e.g. `Cascade`, `SetNull`, `SetDefault`, `Restrict`, `NoAction`).
- `OnDelete(...)` declares the referential action to take on deletes.
- `Comment(...)` adds a comment to the foreign key.

## Index declaration

```csharp
public class AppSchema : AbstractSchemaProvider
{
    public AppSchema()
    {
        Schema("app", s =>
        {
            s.Table("users", t => {
                t.Index("idx_users_email", ["email"], i => i.Unique());
            });
        });
    }
}
```

- `Name` declares the name of the index in the database.
- `ColumnNames` declares the columns that are part of the index.
- `Unique()` marks the index as unique.

## Defining schemas in JSON

Instead of writing a C# class, you can declare the desired schema in a JSON file and register it with `AddJsonSchema`:

```csharp
builder.AddJsonSchema("schema.json");
```

You can register multiple JSON files, mix them with `AbstractSchemaProvider` classes, or use them on their own; all registered providers are aggregated before planning.

To register every JSON file in a directory at once, use `AddJsonSchemasFromDirectory`:

```csharp
builder.AddJsonSchemasFromDirectory("schemas");
```

By default this picks up `*.json` files recursively, including subdirectories.

The document mirrors the schema model:

```json
{
  "schemas": [
    {
      "name": "app",
      "comment": "The application schema.",
      "tables": [
        {
          "name": "users",
          "primaryKey": { "name": "users_pkey", "columnNames": ["id"] },
          "columns": [
            { "name": "id", "type": "bigint", "isIdentity": true },
            { "name": "email", "type": "varchar(255)" },
            { "name": "name", "type": "text" },
            { "name": "balance", "type": "decimal(18,2)", "isNullable": true, "defaultExpression": "0" }
          ],
          "indexes": [
            { "name": "uc_users_email", "columnNames": ["email"], "isUnique": true }
          ],
          "foreignKeys": [
            {
              "name": "FK_users_role_id",
              "columnNames": ["role_id"],
              "referencedSchema": "app",
              "referencedTable": "roles",
              "referencedColumnNames": ["id"],
              "onDelete": "Cascade",
              "onUpdate": "NoAction"
            }
          ]
        }
      ]
    }
  ],
  "droppedSchemas": []
}
```

Renames use `oldName` (the JSON equivalent of `RenamedFrom(...)`) on a schema, table, or column. A schema can be marked partial with `"isPartial": true`, dropped tables are listed in a schema's `droppedTables` array, and dropped schemas in the top-level `droppedSchemas` array. Referential actions (`onDelete` / `onUpdate`) and table privileges are written as their enum names (`Cascade`, `SetNull`, `Select`, `All`, ...).

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

Any string that isn't a recognized built-in type becomes a custom type, which is how you target database-specific types like `jsonb` or `uuid`.
