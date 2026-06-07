using NSchema.Schema.Model;
using NSchema.State;
using NSchema.Tests.Helpers;

namespace NSchema.Tests.State;

public sealed class DefaultSchemaStateSerializerTests
{
    private static readonly ISchemaStateSerializer _sut = new DefaultSchemaStateSerializer();

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

    [Fact]
    public void Serialize_ThenDeserialize_RoundTripsAllFeatures()
    {
        // Arrange
        var original = RichSchema();

        // Act: a read + write cycle must reproduce the exact same document.
        var json = _sut.Serialize(original);
        var roundTripped = _sut.Deserialize(json);

        // Assert
        _sut.Serialize(roundTripped).ShouldBe(json);
    }

    [Theory]
    [MemberData(nameof(SqlTypeHelpers.AllShapes), MemberType = typeof(SqlTypeHelpers))]
    public void RoundTrip_PreservesSqlType(SqlType type)
    {
        // Arrange
        var schema = DatabaseSchema.Create(
            [SchemaDefinition.Create("app", tables: [Table.Create("t", columns: [Column.Create("c", type)])])]);

        // Act
        var roundTripped = _sut.Deserialize(_sut.Serialize(schema));

        // Assert
        roundTripped.Schemas[0].Tables[0].Columns[0].Type.ShouldBe(type);
    }

    [Fact]
    public Task Serialize_MatchesSnapshot()
        // The verified snapshot pins the on-disk format. VerifyJson reformats both sides consistently,
        // so whitespace and indentation don't matter — but a domain change that alters the serialized
        // shape fails here. When that's intentional, accept the .received.txt as the new baseline (and
        // bump SchemaStateEnvelope.CurrentVersion if the on-disk format itself changed).
        => VerifyJson(_sut.Serialize(RichSchema()));

    [Fact]
    public void Serialize_WritesEnumsAsNames()
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
        var json = _sut.Serialize(schema);

        // Assert: enums serialize as readable names, not integers.
        json.ShouldContain("\"Cascade\"");
        json.ShouldNotContain("\"onDelete\": 1");
    }

    [Fact]
    public void Serialize_WritesDefaultAndNullMembers()
    {
        // The state store is a fact store: a member at its default value must still be recorded, so that
        // "absent" can never be mistaken for "present and equal to today's default". This is the inverse
        // of the user-facing serializer, which omits defaults. See DomainModelSerializationContractTests.
        var schema = DatabaseSchema.Create(
        [
            SchemaDefinition.Create("app", tables:
            [
                Table.Create("t", columns: [Column.Create("c", SqlType.Int)]),
            ]),
        ]);

        // Act
        var json = _sut.Serialize(schema);

        // Assert: false bools and null strings are written, not omitted.
        json.ShouldContain("\"isPartial\": false");
        json.ShouldContain("\"isNullable\": false");
        json.ShouldContain("\"isIdentity\": false");
        json.ShouldContain("\"defaultExpression\": null");
    }

    [Fact]
    public void Deserialize_FutureFormatVersion_Throws()
    {
        // Arrange
        const string json =
            """
            { "version": 9999, "schema": { "schemas": [], "droppedSchemas": [] } }
            """;

        // Act
        var act = () => _sut.Deserialize(json);

        // Assert
        act.ShouldThrow<NotSupportedException>();
    }
}
