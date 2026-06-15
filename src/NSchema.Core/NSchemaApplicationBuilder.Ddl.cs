using NSchema.Schema;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
    /// <summary>
    /// Adds a provider that loads the desired schema from the specified NSchema SQL DSL (<c>.sql</c>) file.
    /// </summary>
    /// <param name="filePath">Absolute or relative path to the SQL DSL schema file.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddSqlSchema(string filePath)
    {
        return AddSchema(_ => new DdlSchemaProvider(filePath));
    }

    /// <summary>
    /// Adds a provider for every matching SQL DSL file in a directory.
    /// </summary>
    /// <param name="directoryPath">Absolute or relative path to the directory to scan.</param>
    /// <param name="searchPattern">The search pattern to match files against. Defaults to <c>*.sql</c>.</param>
    /// <param name="recursive">Whether to include matching files in subdirectories. Defaults to <see langword="true"/>.</param>
    /// <returns>The application builder, for chaining.</returns>
    /// <exception cref="DirectoryNotFoundException">The directory does not exist.</exception>
    public NSchemaApplicationBuilder AddSqlSchemasFromDirectory(string directoryPath, string searchPattern = "*.sql", bool recursive = true) =>
        AddFileSchemasFromDirectory(directoryPath, searchPattern, recursive, path => new DdlSchemaProvider(path));

    /// <summary>
    /// Adds a provider for every SQL DSL file matching a glob pattern.
    /// </summary>
    /// <param name="globPattern">A glob pattern, e.g. <c>schemas/**/*.sql</c>.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddSqlSchemasFromGlob(string globPattern) =>
        AddFileSchemasFromGlob(globPattern, path => new DdlSchemaProvider(path));
}
