using NSchema.Schema;

namespace NSchema.Migration;

/// <summary>
/// Base class for an <see cref="ISchemaProvider"/> that loads the desired schema from a single file.
/// </summary>
public abstract class FileSchemaProvider : ISchemaProvider
{
    private readonly string _filePath;

    /// <param name="filePath">Absolute or relative path to the schema file.</param>
    protected FileSchemaProvider(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    /// <summary>
    /// Parses the open file <paramref name="stream"/> into a <see cref="DatabaseSchema"/>.
    /// </summary>
    /// <param name="stream">A readable stream over the schema file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    protected abstract ValueTask<DatabaseSchema> Parse(Stream stream, CancellationToken cancellationToken);

    /// <inheritdoc/>
    public async Task<DatabaseSchema> GetSchema(string[]? schemaNames = null, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            throw new FileNotFoundException($"Schema file not found: \"{_filePath}\".", _filePath);
        }

        await using var stream = File.OpenRead(_filePath);
        var schema = await Parse(stream, cancellationToken);
        return schema.Filter(schemaNames);
    }
}
