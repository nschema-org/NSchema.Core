using System.Text;
using NSchema.Schema.Model;
using NSchema.Schema.Serialization;
using NSchema.Tests.Helpers;

namespace NSchema.Tests.Schema.Serialization;

public sealed class JsonSchemaSerializerTests
{
    private static readonly JsonSchemaSerializer _sut = new();

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

    [Fact]
    public async Task Write_ThenRead_RoundTripsAllFeatures()
    {
        // Arrange
        var original = TestData.RichSchema();

        // Act: a write + read cycle must reproduce the exact same document.
        var json = await Serialize(original);
        var roundTripped = await Deserialize(json);

        // Assert
        (await Serialize(roundTripped)).ShouldBe(json);
    }

    [Theory]
    [MemberData(nameof(SqlTypeHelpers.AllShapes), MemberType = typeof(SqlTypeHelpers))]
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
    public async Task Write_MatchesSnapshot()
        // The verified snapshot pins the on-disk document format. VerifyJson reformats both sides
        // consistently, so whitespace doesn't matter — but a domain change that alters the serialized
        // shape fails here. When that's intentional, accept the .received.txt as the new baseline.
        => await VerifyJson(await Serialize(TestData.RichSchema()));

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
