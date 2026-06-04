using NSchema.Schema.Model;
using NSchema.Schema.Serialization;

namespace NSchema.Schema;

/// <summary>
/// An <see cref="ISchemaProvider"/> that loads the desired schema from a single file.
/// </summary>
public class FileSchemaProvider : ISchemaProvider
{
    private readonly string _filePath;
    private readonly ISchemaDocumentSerializer _serializer;

    /// <param name="filePath">Absolute or relative path to the schema file.</param>
    /// <param name="serializer">The serializer that parses the file's format.</param>
    public FileSchemaProvider(string filePath, ISchemaDocumentSerializer serializer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(serializer);
        _filePath = filePath;
        _serializer = serializer;
    }

    /// <inheritdoc/>
    public async Task<DatabaseSchema> GetSchema(string[]? schemaNames = null, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            throw new FileNotFoundException($"Schema file not found: \"{_filePath}\".", _filePath);
        }

        await using var stream = File.OpenRead(_filePath);
        var schema = await _serializer.Read(stream, cancellationToken);
        return schema.Filter(schemaNames);
    }
}
