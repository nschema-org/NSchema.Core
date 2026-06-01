using System.Text.Json;
using System.Text.Json.Serialization;
using NSchema.Migration;
using NSchema.Schema;

namespace NSchema.Json;

/// <summary>
/// An <see cref="ISchemaProvider"/> that loads the desired schema from a JSON file.
/// </summary>
public sealed class JsonSchemaProvider : ISchemaProvider
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
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

        return Filter(schema, schemaNames);
    }

    private static DatabaseSchema Filter(DatabaseSchema schema, string[]? schemaNames)
    {
        if (schemaNames is not { Length: > 0 })
        {
            return schema;
        }

        var scope = new HashSet<string>(schemaNames, StringComparer.OrdinalIgnoreCase);
        var filtered = schema.Schemas.Where(s => scope.Contains(s.Name)).ToList();
        var filteredDropped = schema.DroppedSchemas.Where(scope.Contains).ToList();
        return new DatabaseSchema(filtered, filteredDropped);
    }
}
