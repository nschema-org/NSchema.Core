using NSchema.Schema.Model;

namespace NSchema.Schema.Ddl;

/// <summary>
/// An <see cref="ISchemaProvider"/> that loads the desired schema from an NSchema SQL DDL (<c>.sql</c>) file.
/// </summary>
internal sealed class DdlSchemaProvider : ISchemaProvider
{
    private readonly string _filePath;

    /// <param name="filePath">Absolute or relative path to the SQL DDL schema file.</param>
    public DdlSchemaProvider(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    /// <inheritdoc/>
    public async ValueTask<DatabaseSchema> GetSchema(string[]? schemaNames = null, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            throw new FileNotFoundException($"Schema file not found: \"{_filePath}\".", _filePath);
        }

        var text = await File.ReadAllTextAsync(_filePath, cancellationToken);
        return DdlReader.Instance.Read(text).Schema.Filter(schemaNames);
    }
}
