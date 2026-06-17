using NSchema.Schema.Serialization.Ddl;

namespace NSchema.Schema;

/// <summary>
/// An <see cref="ISchemaProvider"/> that loads the desired schema from an NSchema SQL DDL (<c>.sql</c>) file.
/// </summary>
internal sealed class DdlSchemaProvider : FileSchemaProvider
{
    /// <param name="filePath">Absolute or relative path to the SQL DDL schema file.</param>
    public DdlSchemaProvider(string filePath)
        : base(filePath, DdlSchemaSerializer.Instance) { }
}
