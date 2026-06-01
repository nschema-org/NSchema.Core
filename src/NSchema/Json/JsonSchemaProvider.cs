using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using NSchema.Migration;
using NSchema.Schema;

namespace NSchema.Json;

/// <summary>
/// An <see cref="ISchemaProvider"/> that loads the desired schema from a JSON file.
/// </summary>
internal sealed class JsonSchemaProvider : FileSchemaProvider
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

    /// <param name="filePath">Absolute or relative path to the JSON schema file.</param>
    public JsonSchemaProvider(string filePath) : base(filePath)
    {
    }

    /// <inheritdoc/>
    protected override async ValueTask<DatabaseSchema> Parse(Stream stream, CancellationToken cancellationToken)
        => await JsonSerializer.DeserializeAsync<DatabaseSchema>(stream, _options, cancellationToken)
        ?? throw new JsonException("Unable to deserialize the schema.");
}
