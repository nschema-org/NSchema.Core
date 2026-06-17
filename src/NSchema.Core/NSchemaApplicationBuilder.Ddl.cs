using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using NSchema.Schema.Ddl;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
    /// <summary>
    /// Adds a provider that loads the desired schema from the specified NSchema SQL DDL (<c>.sql</c>) file.
    /// </summary>
    /// <param name="filePath">Absolute or relative path to the SQL DDL schema file.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddSqlSchema(string filePath)
    {
        return AddSchema(_ => new DdlSchemaProvider(filePath));
    }

    /// <summary>
    /// Adds a provider for every matching SQL DDL file in a directory.
    /// </summary>
    /// <param name="directoryPath">Absolute or relative path to the directory to scan.</param>
    /// <param name="searchPattern">The search pattern to match files against. Defaults to <c>*.sql</c>.</param>
    /// <param name="recursive">Whether to include matching files in subdirectories. Defaults to <see langword="true"/>.</param>
    /// <returns>The application builder, for chaining.</returns>
    /// <exception cref="DirectoryNotFoundException">The directory does not exist.</exception>
    public NSchemaApplicationBuilder AddSqlSchemasFromDirectory(string directoryPath, string searchPattern = "*.sql", bool recursive = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(searchPattern);

        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Schema directory not found: \"{directoryPath}\".");
        }

        var prefix = directoryPath.TrimEnd('/', '\\');
        var globPattern = recursive ? $"{prefix}/**/{searchPattern}" : $"{prefix}/{searchPattern}";
        return AddSqlSchemasFromGlob(globPattern);
    }

    /// <summary>
    /// Adds a provider for every SQL DDL file matching a glob pattern.
    /// </summary>
    /// <param name="globPattern">A glob pattern, e.g. <c>schemas/**/*.sql</c>.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddSqlSchemasFromGlob(string globPattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(globPattern);

        var (root, pattern) = SplitGlobRoot(globPattern);
        var matcher = new Matcher();
        matcher.AddInclude(pattern);

        var matches = matcher
            .Execute(new DirectoryInfoWrapper(new DirectoryInfo(root)))
            .Files
            .Select(match => Path.GetFullPath(match.Path, root))
            .OrderBy(path => path, StringComparer.Ordinal);

        foreach (var file in matches)
        {
            AddSchema(_ => new DdlSchemaProvider(file));
        }

        return this;
    }

    // Splits a glob into the directory it is matched against and the pattern relative to that directory. A relative
    // glob is rooted at the current working directory; a rooted glob is split at the first segment containing a
    // wildcard, so its static leading path becomes the matcher root (Matcher only matches relative patterns).
    private static (string Root, string Pattern) SplitGlobRoot(string globPattern)
    {
        if (!Path.IsPathRooted(globPattern))
        {
            return (System.Environment.CurrentDirectory, globPattern);
        }

        var segments = globPattern.Split('/', '\\');
        var staticCount = 0;
        while (staticCount < segments.Length && !IsGlobSegment(segments[staticCount]))
        {
            staticCount++;
        }

        var root = string.Join(Path.DirectorySeparatorChar, segments.Take(staticCount));
        var pattern = string.Join('/', segments.Skip(staticCount));
        return (root, pattern);

        static bool IsGlobSegment(string segment) => segment.Contains('*') || segment.Contains('?');
    }
}
