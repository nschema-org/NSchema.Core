using NSchema.Json;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
    /// <summary>
    /// Adds a <see cref="JsonSchemaProvider"/> that loads the desired schema from the specified JSON file.
    /// Multiple calls are allowed; each file is treated as a separate provider and aggregated.
    /// </summary>
    /// <param name="filePath">Absolute or relative path to the JSON schema file.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddJsonSchema(string filePath) => AddSchema(_ => new JsonSchemaProvider(filePath));
}
