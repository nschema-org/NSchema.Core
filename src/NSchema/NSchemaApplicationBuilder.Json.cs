using NSchema.Json;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
    /// <summary>
    /// Adds a <see cref="JsonSchemaProvider"/> that loads the desired schema from the specified JSON file.
    /// </summary>
    /// <param name="filePath">Absolute or relative path to the JSON schema file.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddJsonSchema(string filePath)
    {
        return AddSchema(_ => new JsonSchemaProvider(filePath));
    }

    /// <summary>
    /// Adds a <see cref="JsonSchemaProvider"/> for every matching JSON file in a directory.
    /// </summary>
    /// <param name="directoryPath">Absolute or relative path to the directory to scan.</param>
    /// <param name="searchPattern">The search pattern to match files against. Defaults to <c>*.json</c>.</param>
    /// <param name="recursive">Whether to include matching files in subdirectories. Defaults to <see langword="true"/>.</param>
    /// <returns>The application builder, for chaining.</returns>
    /// <exception cref="DirectoryNotFoundException">The directory does not exist.</exception>
    public NSchemaApplicationBuilder AddJsonSchemasFromDirectory(string directoryPath, string searchPattern = "*.json", bool recursive = true) => AddFileSchemasFromDirectory(directoryPath, searchPattern, recursive, path => new JsonSchemaProvider(path));

    /// <summary>
    /// Adds a <see cref="JsonSchemaProvider"/> for every JSON file matching a glob pattern.
    /// </summary>
    /// <param name="globPattern">A glob pattern, e.g. <c>schemas/**/*.json</c>.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddJsonSchemasFromGlob(string globPattern) => AddFileSchemasFromGlob(globPattern, path => new JsonSchemaProvider(path));
}
