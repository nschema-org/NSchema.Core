using NSchema.Schema.Serialization;

namespace NSchema.Schema;

/// <summary>
/// An <see cref="ISchemaProvider"/> that loads the desired schema from a JSON file.
/// </summary>
internal sealed class JsonSchemaProvider : FileSchemaProvider
{
    /// <param name="filePath">Absolute or relative path to the JSON schema file.</param>
    public JsonSchemaProvider(string filePath)
        : base(filePath, JsonSchemaDocumentSerializer.Instance) { }
}
