using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using NSchema.Schema.Model;

namespace NSchema.Schema.Serialization;

/// <summary>
/// Reads and writes a <see cref="DatabaseSchema"/> as an indented, camel-cased JSON document.
/// </summary>
public sealed class JsonSchemaDocumentSerializer : ISchemaDocumentSerializer
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { SqlTypeJsonConverter.SuppressPolymorphism },
        },
        Converters = { new SqlTypeJsonConverter(), new JsonStringEnumConverter() },
    };

    /// <inheritdoc/>
    public string Format => "json";

    /// <inheritdoc/>
    public Task Write(DatabaseSchema schema, Stream destination, CancellationToken cancellationToken = default)
        => JsonSerializer.SerializeAsync(destination, schema, _options, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<DatabaseSchema> Read(Stream source, CancellationToken cancellationToken = default)
        => await JsonSerializer.DeserializeAsync<DatabaseSchema>(source, _options, cancellationToken)
        ?? throw new JsonException("Unable to deserialize the schema.");
}
