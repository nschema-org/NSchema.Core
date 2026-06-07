using System.Text.Json;
using System.Text.Json.Serialization;
using NSchema.Schema.Model;

namespace NSchema.Schema.Serialization;

/// <summary>
/// Serializes <see cref="SqlType"/> as its compact canonical string (e.g. <c>"varchar(255)"</c>).
/// </summary>
internal sealed class SqlTypeJsonConverter : JsonConverter<SqlType>
{
    public override SqlType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString() ?? throw new JsonException($"Expected a string for {nameof(SqlType)} but found null.");
        return SqlType.Parse(value);
    }

    public override void Write(Utf8JsonWriter writer, SqlType value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
