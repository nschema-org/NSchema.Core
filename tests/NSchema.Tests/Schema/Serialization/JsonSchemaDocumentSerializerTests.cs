using System.Text;
using NSchema.Schema.Model;
using NSchema.Schema.Serialization;

namespace NSchema.Tests.Schema.Serialization;

public sealed class JsonSchemaDocumentSerializerTests
{
    private static readonly JsonSchemaDocumentSerializer _sut = new();

    private static async Task<string> Serialize(DatabaseSchema schema)
    {
        using var stream = new MemoryStream();
        await _sut.Write(schema, stream, TestContext.Current.CancellationToken);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static async Task<DatabaseSchema> Deserialize(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return await _sut.Read(stream, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// A schema exercising every domain feature, for round-trip coverage.
    /// </summary>
    private static DatabaseSchema RichSchema() => DatabaseSchema.Create(
        schemas:
        [
            SchemaDefinition.Create(
                name: "app",
                oldName: "legacy_app",
                isPartial: true,
                comment: "application schema",
                tables:
                [
                    Table.Create(
                        name: "users",
                        oldName: "members",
                        primaryKey: new PrimaryKey("users_pkey", ["id"]),
                        comment: "all users",
                        columns:
                        [
                            Column.Create("id", SqlType.BigInt, isIdentity: true,
                                identityOptions: new IdentityOptions(1, 1, 1)),
                            Column.Create("name", SqlType.VarChar(255), comment: "display name"),
                            Column.Create("balance", SqlType.Decimal(18, 2), isNullable: true, defaultExpression: "0"),
                            Column.Create("code", SqlType.Char(8), oldName: "short_code"),
                            Column.Create("metadata", SqlType.Custom("jsonb"), isNullable: true),
                        ],
                        foreignKeys:
                        [
                            ForeignKey.Create("users_org_fk", ["org_id"], "app", "orgs", ["id"],
                                ReferentialAction.Cascade, ReferentialAction.SetNull),
                        ],
                        indexes:
                        [
                            TableIndex.Create("users_name_ix", ["name"], isUnique: true,
                                comment: "unique names", predicate: "name IS NOT NULL"),
                        ],
                        grants: [new TableGrant("readers", TablePrivilege.All)]),
                ],
                droppedTables: ["old_table"],
                grants: [new SchemaGrant("app_role")]),
        ],
        droppedSchemas: ["scratch"]);

    private static readonly SqlType[] _sqlTypes =
    [
        SqlType.Boolean, SqlType.TinyInt, SqlType.SmallInt, SqlType.Int, SqlType.BigInt,
        SqlType.Float, SqlType.Double, SqlType.Text, SqlType.Date, SqlType.Time,
        SqlType.DateTime, SqlType.DateTimeOffset, SqlType.Guid,
        SqlType.Decimal(18, 2), SqlType.Char(8), SqlType.NChar(4), SqlType.Binary(16),
        SqlType.VarChar(255), SqlType.VarChar(), SqlType.NVarChar(64), SqlType.NVarChar(),
        SqlType.VarBinary(32), SqlType.VarBinary(), SqlType.Custom("jsonb"),
    ];

    public static TheoryData<SqlType> AllSqlTypes()
    {
        var data = new TheoryData<SqlType>();
        foreach (var type in _sqlTypes)
        {
            data.Add(type);
        }
        return data;
    }

    [Fact]
    public void Format_IsJson() => _sut.Format.ShouldBe("json");

    [Fact]
    public async Task Write_ThenRead_RoundTripsAllFeatures()
    {
        // Arrange
        var original = RichSchema();

        // Act: a write + read cycle must reproduce the exact same document.
        var json = await Serialize(original);
        var roundTripped = await Deserialize(json);

        // Assert
        (await Serialize(roundTripped)).ShouldBe(json);
    }

    [Theory]
    [MemberData(nameof(AllSqlTypes))]
    public async Task RoundTrip_PreservesSqlType(SqlType type)
    {
        // Arrange
        var schema = DatabaseSchema.Create(
            [SchemaDefinition.Create("app", tables: [Table.Create("t", columns: [Column.Create("c", type)])])]);

        // Act
        var roundTripped = await Deserialize(await Serialize(schema));

        // Assert
        roundTripped.Schemas[0].Tables[0].Columns[0].Type.ShouldBe(type);
    }

    [Fact]
    public void AllSqlTypes_CoversEveryConcreteSqlType()
    {
        // Every concrete SqlType in the domain must appear in AllSqlTypes, so RoundTrip_PreservesSqlType
        // exercises it. Declaring a new SqlType without adding it here will fail this test.
        var declared = typeof(SqlType).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(SqlType)))
            .ToHashSet();

        var covered = _sqlTypes.Select(type => type.GetType()).ToHashSet();

        var missing = declared.Except(covered).Select(type => type.Name).Order().ToList();
        missing.ShouldBeEmpty($"AllSqlTypes is missing: {string.Join(", ", missing)}");
    }

    [Fact]
    public async Task Write_MatchesSnapshot()
        // The verified snapshot pins the on-disk document format. VerifyJson reformats both sides
        // consistently, so whitespace doesn't matter — but a domain change that alters the serialized
        // shape fails here. When that's intentional, accept the .received.txt as the new baseline.
        => await VerifyJson(await Serialize(RichSchema()));

    [Fact]
    public async Task Write_WritesEnumsAsNames()
    {
        // Arrange
        var schema = DatabaseSchema.Create(
        [
            SchemaDefinition.Create("app", tables:
            [
                Table.Create("users", foreignKeys:
                [
                    ForeignKey.Create("fk", ["org_id"], "app", "orgs", ["id"], ReferentialAction.Cascade),
                ]),
            ]),
        ]);

        // Act
        var json = await Serialize(schema);

        // Assert: enums serialize as readable names, not integers.
        json.ShouldContain("\"Cascade\"");
        json.ShouldNotContain("\"onDelete\": 1");
    }

    [Fact]
    public async Task Write_WritesSqlTypeAsCompactString()
    {
        // Arrange
        var schema = DatabaseSchema.Create(
            [SchemaDefinition.Create("app", tables: [Table.Create("t", columns: [Column.Create("c", SqlType.VarChar(255))])])]);

        // Act
        var json = await Serialize(schema);

        // Assert: SqlType uses its canonical scalar form, not the polymorphic object shape.
        json.ShouldContain("\"varchar(255)\"");
    }

    [Fact]
    public async Task Read_NullPayload_Throws()
    {
        var act = () => Deserialize("null");

        await act.ShouldThrowAsync<System.Text.Json.JsonException>();
    }
}
