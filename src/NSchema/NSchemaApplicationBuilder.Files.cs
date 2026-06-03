using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using NSchema.Schema;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
    /// <summary>
    /// Registers an <see cref="ISchemaProvider"/> for every matching file in a directory.
    /// </summary>
    /// <param name="directoryPath">Absolute or relative path to the directory to scan.</param>
    /// <param name="searchPattern">The pattern to match files against, e.g. <c>*.json</c>.</param>
    /// <param name="recursive">Whether to include matching files in subdirectories.</param>
    /// <param name="providerFactory">Creates a provider for a single matched file path.</param>
    /// <returns>The application builder, for chaining.</returns>
    /// <exception cref="DirectoryNotFoundException">The directory does not exist.</exception>
    public NSchemaApplicationBuilder AddFileSchemasFromDirectory(
        string directoryPath,
        string searchPattern,
        bool recursive,
        Func<string, ISchemaProvider> providerFactory
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(searchPattern);
        ArgumentNullException.ThrowIfNull(providerFactory);

        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Schema directory not found: \"{directoryPath}\".");
        }

        var prefix = directoryPath.TrimEnd('/', '\\');
        var globPattern = recursive ? $"{prefix}/**/{searchPattern}" : $"{prefix}/{searchPattern}";
        return AddFileSchemasFromGlob(globPattern, providerFactory);
    }

    /// <summary>
    /// Registers an <see cref="ISchemaProvider"/> (built by <paramref name="providerFactory"/>) for every file matching a glob pattern.
    /// </summary>
    /// <param name="globPattern">A glob pattern, e.g. <c>schemas/**/*.json</c>.</param>
    /// <param name="providerFactory">Creates a provider for a single matched file path.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddFileSchemasFromGlob(string globPattern, Func<string, ISchemaProvider> providerFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(globPattern);
        ArgumentNullException.ThrowIfNull(providerFactory);

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
            AddSchema(_ => providerFactory(file));
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
