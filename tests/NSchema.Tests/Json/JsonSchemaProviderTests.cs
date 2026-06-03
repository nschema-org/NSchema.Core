using System.Text.Json;
using NSchema.Migration.Sources;
using NSchema.Schema;

namespace NSchema.Tests.Json;

public sealed class JsonSchemaProviderTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public JsonSchemaProviderTests() => Directory.CreateDirectory(_tempDir);
    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    // -------------------------------------------------------------------------
    // Happy-path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetSchema_ReturnsEmptyDatabase_WhenFileHasNoSchemas()
    {
        var path = WriteFile("empty.json", """{ "schemas": [], "droppedSchemas": [] }""");
        var sut = new JsonSchemaProvider(path);

        var result = await sut.GetSchema(null, TestContext.Current.CancellationToken);

        result.Schemas.ShouldBeEmpty();
        result.DroppedSchemas.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetSchema_DeserializesSchemaAndTable()
    {
        var path = WriteFile("basic.json",
            """
            {
              "schemas": [
                {
                  "name": "app",
                  "tables": [
                    {
                      "name": "users",
                      "columns": [
                        { "name": "id",   "type": "int",  "isNullable": false },
                        { "name": "name", "type": "text", "isNullable": true  }
                      ]
                    }
                  ]
                }
              ],
              "droppedSchemas": []
            }
            """);

        var sut = new JsonSchemaProvider(path);
        var result = await sut.GetSchema(null, TestContext.Current.CancellationToken);

        result.Schemas.Count.ShouldBe(1);
        var schema = result.Schemas[0];
        schema.Name.ShouldBe("app");

        var table = schema.Tables[0];
        table.Name.ShouldBe("users");
        table.Columns.Count.ShouldBe(2);
        table.Columns[0].Type.ShouldBe(SqlType.Int);
        table.Columns[1].IsNullable.ShouldBeTrue();
    }

    [Fact]
    public async Task GetSchema_DeserializesRichFeatures()
    {
        var path = WriteFile("rich.json",
            """
            {
              "schemas": [
                {
                  "name": "app",
                  "oldName": "legacy",
                  "comment": "main schema",
                  "tables": [
                    {
                      "name": "orders",
                      "oldName": "order_items",
                      "primaryKey": { "name": "orders_pkey", "columnNames": ["id"] },
                      "columns": [
                        { "name": "id",     "type": "bigint", "isIdentity": true,
                          "identityOptions": { "startWith": 1, "minValue": 1, "incrementBy": 1 } },
                        { "name": "total",  "type": "decimal(18,2)", "isNullable": false,
                          "defaultExpression": "0" },
                        { "name": "code",   "type": "char(8)", "oldName": "short_code" },
                        { "name": "meta",   "type": "jsonb",   "isNullable": true }
                      ],
                      "indexes": [
                        { "name": "orders_code_ux", "columnNames": ["code"], "isUnique": true }
                      ],
                      "foreignKeys": [
                        {
                          "name": "orders_user_fk",
                          "columnNames": ["user_id"],
                          "referencedSchema": "app",
                          "referencedTable": "users",
                          "referencedColumnNames": ["id"],
                          "onDelete": "Cascade",
                          "onUpdate": "NoAction"
                        }
                      ],
                      "grants": [
                        { "role": "reader", "privileges": "Select" }
                      ]
                    }
                  ],
                  "droppedTables": ["old_orders"],
                  "grants": [{ "role": "app_role" }]
                }
              ],
              "droppedSchemas": ["scratch"]
            }
            """);

        var sut = new JsonSchemaProvider(path);
        var result = await sut.GetSchema(null, TestContext.Current.CancellationToken);

        var schema = result.Schemas[0];
        schema.Name.ShouldBe("app");
        schema.OldName.ShouldBe("legacy");
        schema.Comment.ShouldBe("main schema");
        schema.Grants[0].Role.ShouldBe("app_role");
        schema.DroppedTables.ShouldContain("old_orders");
        result.DroppedSchemas.ShouldContain("scratch");

        var table = schema.Tables[0];
        table.Name.ShouldBe("orders");
        table.OldName.ShouldBe("order_items");
        table.PrimaryKey!.Name.ShouldBe("orders_pkey");
        table.PrimaryKey.ColumnNames.ShouldBe(["id"]);

        var idCol = table.Columns[0];
        idCol.Type.ShouldBe(SqlType.BigInt);
        idCol.IsIdentity.ShouldBeTrue();
        idCol.IdentityOptions.ShouldNotBeNull();
        idCol.IdentityOptions!.StartWith.ShouldBe(1);

        table.Columns[1].Type.ShouldBe(SqlType.Decimal(18, 2));
        table.Columns[1].DefaultExpression.ShouldBe("0");
        table.Columns[2].Type.ShouldBe(SqlType.Char(8));
        table.Columns[2].OldName.ShouldBe("short_code");
        table.Columns[3].Type.ShouldBe(SqlType.Custom("jsonb"));

        var index = table.Indexes[0];
        index.Name.ShouldBe("orders_code_ux");
        index.IsUnique.ShouldBeTrue();

        var fk = table.ForeignKeys[0];
        fk.Name.ShouldBe("orders_user_fk");
        fk.OnDelete.ShouldBe(ReferentialAction.Cascade);
        fk.OnUpdate.ShouldBe(ReferentialAction.NoAction);

        table.Grants[0].Role.ShouldBe("reader");
    }

    // -------------------------------------------------------------------------
    // SqlType string parsing
    // -------------------------------------------------------------------------

    public static TheoryData<string, SqlType> SqlTypeStringCases() => new()
    {
        { "boolean",        SqlType.Boolean },
        { "tinyint",        SqlType.TinyInt },
        { "smallint",       SqlType.SmallInt },
        { "int",            SqlType.Int },
        { "bigint",         SqlType.BigInt },
        { "float",          SqlType.Float },
        { "double",         SqlType.Double },
        { "text",           SqlType.Text },
        { "date",           SqlType.Date },
        { "time",           SqlType.Time },
        { "datetime",       SqlType.DateTime },
        { "datetimeoffset", SqlType.DateTimeOffset },
        { "guid",           SqlType.Guid },
        { "varchar",        SqlType.VarChar() },
        { "varchar(255)",   SqlType.VarChar(255) },
        { "nvarchar",       SqlType.NVarChar() },
        { "nvarchar(64)",   SqlType.NVarChar(64) },
        { "char(8)",        SqlType.Char(8) },
        { "nchar(4)",       SqlType.NChar(4) },
        { "binary(16)",     SqlType.Binary(16) },
        { "varbinary",      SqlType.VarBinary() },
        { "varbinary(32)",  SqlType.VarBinary(32) },
        { "decimal(10,2)",  SqlType.Decimal(10, 2) },
        { "decimal(18, 4)", SqlType.Decimal(18, 4) },
        { "jsonb",          SqlType.Custom("jsonb") },
        { "uuid",           SqlType.Custom("uuid") },
    };

    [Theory]
    [MemberData(nameof(SqlTypeStringCases))]
    public void SqlTypeParse_ParsesAllTypes(string input, SqlType expected)
        => SqlType.Parse(input).ShouldBe(expected);

    [Fact]
    public void SqlTypeStringCases_CoversEveryConcreteSqlType()
    {
        // Every concrete SqlType must appear in SqlTypeStringCases so SqlTypeParse_ParsesAllTypes
        // and GetSchema_RoundTripsAllSqlTypes exercise its string round-trip. Declaring a new SqlType
        // without adding a Parse case here will fail this test.
        var declared = typeof(SqlType).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(SqlType)))
            .ToHashSet();

        var covered = SqlTypeStringCases()
            .Select(row => row.Data.Item2.GetType())
            .ToHashSet();

        var missing = declared.Except(covered).Select(type => type.Name).Order().ToList();
        missing.ShouldBeEmpty($"SqlTypeStringCases is missing: {string.Join(", ", missing)}");
    }

    [Theory]
    [MemberData(nameof(SqlTypeStringCases))]
    public async Task GetSchema_RoundTripsAllSqlTypes(string typeString, SqlType expected)
    {
        var path = WriteFile($"type-{Guid.NewGuid()}.json",
            $$"""
            {
              "schemas": [{
                "name": "s",
                "tables": [{ "name": "t", "columns": [{ "name": "c", "type": "{{typeString}}" }] }]
              }],
              "droppedSchemas": []
            }
            """);

        var sut = new JsonSchemaProvider(path);
        var result = await sut.GetSchema(null, TestContext.Current.CancellationToken);

        result.Schemas[0].Tables[0].Columns[0].Type.ShouldBe(expected);
    }

    // -------------------------------------------------------------------------
    // Schema name filtering
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetSchema_WithSchemaNames_FiltersSchemas()
    {
        var path = WriteFile("multi.json",
            """
            {
              "schemas": [
                { "name": "app",    "tables": [] },
                { "name": "audit",  "tables": [] },
                { "name": "legacy", "tables": [] }
              ],
              "droppedSchemas": ["old", "scratch"]
            }
            """);

        var sut = new JsonSchemaProvider(path);
        var result = await sut.GetSchema(schemaNames: ["app", "old"], TestContext.Current.CancellationToken);

        result.Schemas.Select(s => s.Name).ShouldBe(["app"]);
        result.DroppedSchemas.ShouldBe(["old"]);
    }

    [Fact]
    public async Task GetSchema_WithSchemaNames_IsCaseInsensitive()
    {
        var path = WriteFile("case.json",
            """
            { "schemas": [{ "name": "App", "tables": [] }], "droppedSchemas": [] }
            """);

        var sut = new JsonSchemaProvider(path);
        var result = await sut.GetSchema(schemaNames: ["app"], TestContext.Current.CancellationToken);

        result.Schemas.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetSchema_WithNullSchemaNames_ReturnsAllSchemas()
    {
        var path = WriteFile("all.json",
            """
            {
              "schemas": [{ "name": "a", "tables": [] }, { "name": "b", "tables": [] }],
              "droppedSchemas": []
            }
            """);

        var sut = new JsonSchemaProvider(path);
        var result = await sut.GetSchema(schemaNames: null, TestContext.Current.CancellationToken);

        result.Schemas.Count.ShouldBe(2);
    }

    // -------------------------------------------------------------------------
    // Error cases
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetSchema_WhenFileDoesNotExist_ThrowsFileNotFoundException()
    {
        var sut = new JsonSchemaProvider(Path.Combine(_tempDir, "missing.json"));

        var act = () => sut.GetSchema();

        await act.ShouldThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task GetSchema_WhenJsonIsInvalid_ThrowsJsonException()
    {
        var path = WriteFile("bad.json", "{ not valid json }");
        var sut = new JsonSchemaProvider(path);

        var act = () => sut.GetSchema();

        await act.ShouldThrowAsync<JsonException>();
    }
}
