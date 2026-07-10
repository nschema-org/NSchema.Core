using System.Text;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.Schemas;
using NSchema.Schema.Model.Tables;
using NSchema.State;
using NSchema.State.Model;
using NSchema.Tests.Helpers;

namespace NSchema.Tests.State;

public sealed class SchemaStateSerializerTests
{
    private static readonly ISchemaStateSerializer _sut = new SchemaStateSerializer();

    private static string Json(DatabaseSchema schema) => Encoding.UTF8.GetString(_sut.Serialize(new SchemaState(schema)).Span);

    [Fact]
    public void Serialize_ThenDeserialize_RoundTripsAllFeatures()
    {
        // Arrange
        var original = TestData.RichSchema();

        // Act: a read + write cycle must reproduce the exact same document.
        var json = Json(original);
        var roundTripped = _sut.Deserialize(_sut.Serialize(new SchemaState(original))).Schema;

        // Assert
        Json(roundTripped).ShouldBe(json);
    }

    [Theory]
    [MemberData(nameof(SqlTypeHelpers.AllShapes), MemberType = typeof(SqlTypeHelpers))]
    public void RoundTrip_PreservesSqlType(SqlType type)
    {
        // Arrange
        var schema = new DatabaseSchema(
            [new SchemaDefinition("app", Tables: [new Table("t", Columns: [new Column("c", type)])])]);

        // Act
        var roundTripped = _sut.Deserialize(_sut.Serialize(new SchemaState(schema))).Schema;

        // Assert
        roundTripped.Schemas[0].Tables[0].Columns[0].Type.ShouldBe(type);
    }

    [Fact]
    public Task Serialize_MatchesSnapshot()
        // The verified snapshot pins the on-disk format. VerifyJson reformats both sides consistently,
        // so whitespace and indentation don't matter — but a domain change that alters the serialized
        // shape fails here. When that's intentional, accept the .received.txt as the new baseline (and
        // bump SchemaStateEnvelope.CurrentVersion if the on-disk format itself changed).
        => VerifyJson(Json(TestData.RichSchema()));

    [Fact]
    public void Serialize_WritesEnumsAsNames()
    {
        // Arrange
        var schema = new DatabaseSchema(
        [
            new SchemaDefinition("app", Tables:
            [
                new Table("users", ForeignKeys:
                [
                    new ForeignKey("fk", ["org_id"], "app", "orgs", ["id"], ReferentialAction.Cascade),
                ]),
            ]),
        ]);

        // Act
        var json = Json(schema);

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
        var schema = new DatabaseSchema(
        [
            new SchemaDefinition("app", Tables:
            [
                new Table("t", Columns: [new Column("c", SqlType.Int)]),
            ]),
        ]);

        // Act
        var json = Json(schema);

        // Assert: false bools and null strings are written, not omitted.
        json.ShouldContain("\"isPartial\": false");
        json.ShouldContain("\"isNullable\": false");
        json.ShouldContain("\"isIdentity\": false");
        json.ShouldContain("\"defaultExpression\": null");
    }

    [Fact]
    public void Deserialize_PayloadWithoutEnumOrSequenceCollections_ProducesEmptyCollections()
    {
        // A state file written before enums/sequences existed must still load (the collections default empty).
        const string json =
            """
            {
              "version": 1,
              "schema": {
                "schemas": [ { "name": "app", "tables": [] } ],
                "droppedSchemas": []
              }
            }
            """;

        var schema = _sut.Deserialize(Encoding.UTF8.GetBytes(json)).Schema.Schemas.ShouldHaveSingleItem();

        schema.Enums.ShouldBeEmpty();
        schema.DroppedEnums.ShouldBeEmpty();
        schema.Sequences.ShouldBeEmpty();
        schema.DroppedSequences.ShouldBeEmpty();
    }

    [Fact]
    public void RoundTrip_PreservesExecutedScripts()
    {
        // Arrange
        var executed = new ScriptExecutionRecord("api-login", "abc123", new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero));
        var state = new SchemaState(new DatabaseSchema([new SchemaDefinition("app")]), [executed]);

        // Act
        var roundTripped = _sut.Deserialize(_sut.Serialize(state));

        // Assert
        roundTripped.ExecutedScripts.ShouldHaveSingleItem().ShouldBe(executed);
    }

    [Fact]
    public void Deserialize_PayloadWithoutExecutedScripts_ReadsAsAnEmptyLedger()
    {
        // A state file written before the ledger existed must still load.
        const string json =
            """
            { "version": 1, "schema": { "schemas": [], "droppedSchemas": [] } }
            """;

        _sut.Deserialize(Encoding.UTF8.GetBytes(json)).ExecutedScripts.ShouldBeEmpty();
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
        var act = () => _sut.Deserialize(Encoding.UTF8.GetBytes(json));

        // Assert
        act.ShouldThrow<NotSupportedException>();
    }

    [Fact]
    public void Deserialize_MalformedJson_ThrowsStateDeserializationException()
    {
        // Arrange: not valid JSON at all.
        var act = () => _sut.Deserialize(Encoding.UTF8.GetBytes("{ not json"));

        // Act + Assert: a parse failure surfaces as the dedicated exception with the cause preserved.
        var ex = act.ShouldThrow<StateDeserializationException>();
        ex.InnerException.ShouldNotBeNull();
    }

    [Fact]
    public void Deserialize_StructurallyValidButInvalidModel_ThrowsStateDeserializationException()
    {
        // Arrange: well-formed JSON whose column type has no name, so SqlType's constructor would NRE
        // deep inside the parameterized-constructor converter. The dedicated exception wraps that.
        const string json =
            """
            {
              "version": 1,
              "schema": {
                "schemas": [
                  { "name": "app", "tables": [
                    { "name": "t", "columns": [ { "name": "c", "type": {} } ] }
                  ] }
                ],
                "droppedSchemas": []
              }
            }
            """;

        // Act
        var act = () => _sut.Deserialize(Encoding.UTF8.GetBytes(json));

        // Assert: the caller gets a meaningful exception instead of a bare NullReferenceException.
        var ex = act.ShouldThrow<StateDeserializationException>();
        ex.InnerException.ShouldNotBeNull();
    }
}
