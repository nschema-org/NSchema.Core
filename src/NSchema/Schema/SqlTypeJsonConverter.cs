using System.Text.Json;
using System.Text.Json.Serialization;

namespace NSchema.Schema;

/// <summary>
/// Serializes <see cref="SqlType"/> as a compact string (<c>"int"</c>, <c>"varchar(255)"</c>,
/// <c>"decimal(10,2)"</c>, etc.) by delegating to <see cref="SqlType.ToString"/> and
/// <see cref="SqlType.Parse"/>. Shared by every JSON representation of the schema model.
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
