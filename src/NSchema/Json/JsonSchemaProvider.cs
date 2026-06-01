using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using NSchema.Migration;
using NSchema.Schema;

namespace NSchema.Json;

/// <summary>
/// An <see cref="ISchemaProvider"/> that loads the desired schema from a JSON file.
/// </summary>
internal sealed class JsonSchemaProvider : ISchemaProvider
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { SqlTypeJsonConverter.SuppressPolymorphism },
        },
        Converters = { new SqlTypeJsonConverter(), new JsonStringEnumConverter() },
    };

    private readonly string _filePath;

    /// <param name="filePath">Absolute or relative path to the JSON schema file.</param>
    public JsonSchemaProvider(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    /// <inheritdoc/>
    public async Task<DatabaseSchema> GetSchema(string[]? schemaNames = null, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            throw new FileNotFoundException($"JSON schema file not found: \"{_filePath}\".", _filePath);
        }

        await using var stream = File.OpenRead(_filePath);
        var schema = await JsonSerializer.DeserializeAsync<DatabaseSchema>(stream, _options, cancellationToken)
            ?? throw new JsonException($"JSON schema file deserialized to null: \"{_filePath}\".");

        return schema.Filter(schemaNames);
    }
}
