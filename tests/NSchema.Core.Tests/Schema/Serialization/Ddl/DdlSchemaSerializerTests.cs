using System.Text;
using NSchema.Schema.Model;
using NSchema.Schema.Serialization.Ddl;
using NSchema.Schema.Serialization.Json;
using NSchema.Tests.Helpers;

namespace NSchema.Tests.Schema.Serialization.Ddl;

public sealed class DdlSchemaSerializerTests
{
    private static async Task<string> Json(DatabaseSchema schema)
    {
        using var stream = new MemoryStream();
        await JsonSchemaSerializer.Instance.Write(schema, stream, TestContext.Current.CancellationToken);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    [Fact]
    public async Task WriteThenRead_RoundTripsThroughStream()
    {
        var original = TestData.RichSchema();

        using var stream = new MemoryStream();
        await DdlSchemaSerializer.Instance.Write(original, stream, TestContext.Current.CancellationToken);
        stream.Position = 0;
        var read = await DdlSchemaSerializer.Instance.Read(stream, TestContext.Current.CancellationToken);

        (await Json(read)).ShouldBe(await Json(original));
    }

    [Fact]
    public async Task Read_ParsesDdlFromStream()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("CREATE SCHEMA app; CREATE TABLE app.users (id int NOT NULL);"));
        var schema = await DdlSchemaSerializer.Instance.Read(stream, TestContext.Current.CancellationToken);

        var definition = schema.Schemas.ShouldHaveSingleItem();
        definition.Name.ShouldBe("app");
        definition.Tables.ShouldHaveSingleItem().Name.ShouldBe("users");
    }
}
