using Microsoft.Extensions.DependencyInjection;
using NSchema.Json;
using NSchema.Migration;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
    /// <summary>
    /// Adds a <see cref="JsonSchemaProvider"/> that loads the desired schema from the specified JSON file.
    /// Multiple calls are allowed; each file is treated as a separate provider and aggregated.
    /// </summary>
    /// <param name="filePath">Absolute or relative path to the JSON schema file.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddJsonSchema(string filePath)
    {
        // Registered directly rather than via AddSchema(factory): that uses TryAddEnumerable, which
        // deduplicates by implementation type and would collapse multiple JsonSchemaProviders into one.
        Services.AddSingleton<ISchemaProvider>(new JsonSchemaProvider(filePath));
        return this;
    }

    /// <summary>
    /// Adds a <see cref="JsonSchemaProvider"/> for every matching JSON file in a directory. Each file is
    /// registered as a separate provider and aggregated, exactly as if passed to <see cref="AddJsonSchema"/>.
    /// </summary>
    /// <param name="directoryPath">Absolute or relative path to the directory to scan.</param>
    /// <param name="searchPattern">The search pattern to match files against. Defaults to <c>*.json</c>.</param>
    /// <param name="recursive">Whether to include matching files in subdirectories. Defaults to <see langword="true"/>.</param>
    /// <returns>The application builder, for chaining.</returns>
    /// <exception cref="DirectoryNotFoundException">The directory does not exist.</exception>
    public NSchemaApplicationBuilder AddJsonSchemasFromDirectory(string directoryPath, string searchPattern = "*.json", bool recursive = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"JSON schema directory not found: \"{directoryPath}\".");
        }

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory
            .EnumerateFiles(directoryPath, searchPattern, searchOption)
            .OrderBy(path => path, StringComparer.Ordinal);

        foreach (var file in files)
        {
            AddJsonSchema(file);
        }

        return this;
    }
}
