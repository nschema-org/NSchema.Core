using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace NSchema.Schema.Model;

/// <summary>
/// Serializes <see cref="SqlType"/> as a compact string.
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

    /// <summary>
    /// A <see cref="DefaultJsonTypeInfoResolver"/> modifier that strips the attribute-based polymorphism
    /// from <see cref="SqlType"/> so this converter can take over the string representation.
    /// </summary>
    public static void SuppressPolymorphism(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type == typeof(SqlType))
        {
            typeInfo.PolymorphismOptions = null;
        }
    }
}
