using System.Text;
using NSchema.State.Domain.Models;
using NSchema.State;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Scripts;
using NSchema.Project.Domain.Models.Columns;
using NSchema.Project.Domain.Models.Schemas;
using NSchema.Project.Domain.Models.Tables;

namespace NSchema.Tests.State;

public sealed class DatabaseStateSerializerTests
{
    private static readonly IDatabaseStateSerializer _sut = new DatabaseStateSerializer();

    private static string Json(Database schema) => Encoding.UTF8.GetString(_sut.Serialize(new DatabaseState(schema)).Span);

    [Fact]
    public void Serialize_ThenDeserialize_RoundTripsAllFeatures()
    {
        // Arrange
        var original = TestData.RichSchema();

        // Act: a read + write cycle must reproduce the exact same document.
        var json = Json(original);
        var roundTripped = _sut.Deserialize(_sut.Serialize(new DatabaseState(original))).Database;

        // Assert
        Json(roundTripped).ShouldBe(json);
    }

    [Theory]
    [MemberData(nameof(SqlTypeHelpers.AllShapes), MemberType = typeof(SqlTypeHelpers))]
    public void RoundTrip_PreservesSqlType(SqlType type)
    {
        // Arrange
        var schema = new Database(
            [new Schema(new SqlIdentifier("app"), Tables: [new Table(new SqlIdentifier("t"), Columns: [new Column(new SqlIdentifier("c"), type)])])]);

        // Act
        var roundTripped = _sut.Deserialize(_sut.Serialize(new DatabaseState(schema))).Database;

        // Assert
        roundTripped.Schemas[0].Tables[0].Columns[0].Type.ShouldBe(type);
    }

    [Fact]
    public Task Serialize_MatchesSnapshot()
        // The verified snapshot pins the on-disk format. VerifyJson reformats both sides consistently,
        // so whitespace and indentation don't matter — but a domain change that alters the serialized
        // shape fails here. When that's intentional, accept the .received.txt as the new baseline (and
        // bump DatabaseStateEnvelope.CurrentVersion if the on-disk format itself changed).
        => VerifyJson(Json(TestData.RichSchema()));

    [Fact]
    public void Serialize_WritesEnumsAsNames()
    {
        // Arrange
        var schema = new Database(
        [
            new Schema(new SqlIdentifier("app"), Tables:
            [
                new Table(new SqlIdentifier("users"), ForeignKeys:
                [
                    new ForeignKey(new SqlIdentifier("fk"), [new SqlIdentifier("org_id")], new SqlIdentifier("app"), new SqlIdentifier("orgs"), [new SqlIdentifier("id")], ReferentialAction.Cascade),
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
        var schema = new Database(
        [
            new Schema(new SqlIdentifier("app"), Tables:
            [
                new Table(new SqlIdentifier("t"), Columns: [new Column(new SqlIdentifier("c"), SqlType.Int)]),
            ]),
        ]);

        // Act
        var json = Json(schema);

        // Assert: false bools and null strings are written, not omitted.
        json.ShouldContain("\"isNullable\": false");
        json.ShouldContain("\"isIdentity\": false");
        json.ShouldContain("\"defaultExpression\": null");
    }

    [Fact]
    public void RoundTrip_PreservesExecutedScripts()
    {
        // Arrange
        var executed = new ScriptExecution(new ScriptReference(null, new SqlIdentifier("api-login")), "abc123", new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero));
        var state = new DatabaseState(new Database([new Schema(new SqlIdentifier("app"))]), [executed]);

        // Act
        var roundTripped = _sut.Deserialize(_sut.Serialize(state));

        // Assert
        roundTripped.Scripts.ShouldHaveSingleItem().ShouldBe(executed);
    }

    [Fact]
    public void Serialize_WritesTheLedgerAsScripts()
    {
        // Pins the wire field name — renaming it silently empties every existing ledger.
        var state = new DatabaseState(
            new Database([]),
            [new ScriptExecution(new ScriptReference(null, new SqlIdentifier("api-login")), "abc123", DateTimeOffset.UnixEpoch)]);

        var json = Encoding.UTF8.GetString(_sut.Serialize(state).Span);

        json.ShouldContain("\"scripts\"");
    }

    [Fact]
    public Task Serialize_Ledger_MatchesSnapshot()
        // Pins the ledger entry's wire shape: the script address is structural ({schema, name}, schema null
        // when the script is global), beside the hash and timestamp.
        => VerifyJson(Encoding.UTF8.GetString(_sut.Serialize(new DatabaseState(new Database([]), [
            new ScriptExecution(new ScriptReference(null, new SqlIdentifier("api-login")), "abc123", DateTimeOffset.UnixEpoch),
            new ScriptExecution(new ScriptReference(new SqlIdentifier("sales"), new SqlIdentifier("seed")), "def456", DateTimeOffset.UnixEpoch),
        ])).Span));

    [Fact]
    public void Deserialize_PayloadWithoutScripts_ReadsAsAnEmptyLedger()
    {
        // A state file written before the ledger existed must still load.
        const string json =
            """
            { "version": 1, "schema": { "schemas": [], "droppedSchemas": [] } }
            """;

        _sut.Deserialize(Encoding.UTF8.GetBytes(json)).Scripts.ShouldBeEmpty();
    }

    [Fact]
    public void Deserialize_LegacyExecutedScriptsField_ReadsAsAnEmptyLedger()
    {
        // The 4.x field name is deliberately not read (no dual-read shim): a pre-5.0 ledger is refreshed or
        // untainted by hand under the state-format compat policy's major-version rules.
        const string json =
            """
            {
              "version": 1,
              "schema": { "schemas": [], "droppedSchemas": [] },
              "executedScripts": [ { "name": "api-login", "hash": "abc123", "executedUtc": "2026-07-10T12:00:00+00:00" } ]
            }
            """;

        _sut.Deserialize(Encoding.UTF8.GetBytes(json)).Scripts.ShouldBeEmpty();
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
              "database": {
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
