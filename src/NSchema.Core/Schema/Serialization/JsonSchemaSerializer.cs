using System.Text.Json;
using System.Text.Json.Serialization;
using NSchema.Schema.Model;

namespace NSchema.Schema.Serialization;

/// <summary>
/// Reads and writes a <see cref="DatabaseSchema"/> as an indented, camel-cased JSON document.
/// </summary>
public sealed class JsonSchemaSerializer : ISchemaSerializer
{
    /// <summary>
    /// The format name key for this serializer.
    /// </summary>
    public const string FormatName = "json";

    /// <summary>
    /// A singleton instance of the <see cref="JsonSchemaSerializer"/> class.
    /// </summary>
    public static readonly JsonSchemaSerializer Instance = new();

    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        // SqlType renders as its compact canonical string here; the state store keeps the structural form.
        Converters = { new SqlTypeJsonConverter(), new JsonStringEnumConverter() },
    };

    /// <inheritdoc/>
    public ValueTask Write(DatabaseSchema schema, Stream destination, CancellationToken cancellationToken = default)
        => new(JsonSerializer.SerializeAsync(destination, schema, _options, cancellationToken));

    /// <inheritdoc/>
    public async ValueTask<DatabaseSchema> Read(Stream source, CancellationToken cancellationToken = default)
        => await JsonSerializer.DeserializeAsync<DatabaseSchema>(source, _options, cancellationToken)
        ?? throw new JsonException("Unable to deserialize the schema.");
}
