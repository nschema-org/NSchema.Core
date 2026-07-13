using System.Text.Json;
using System.Text.Json.Serialization;
using NSchema.Project.Domain.Models;

namespace NSchema.Project.Domain;

internal sealed class SqlIdentifierJsonConverter : JsonConverter<SqlIdentifier>
{
    public override SqlIdentifier Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetString() ?? throw new JsonException("An identifier must be a string."));

    public override void Write(Utf8JsonWriter writer, SqlIdentifier value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);

    public override SqlIdentifier ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetString() ?? throw new JsonException("An identifier must be a string."));

    public override void WriteAsPropertyName(Utf8JsonWriter writer, SqlIdentifier value, JsonSerializerOptions options) =>
        writer.WritePropertyName(value.Value);
}
