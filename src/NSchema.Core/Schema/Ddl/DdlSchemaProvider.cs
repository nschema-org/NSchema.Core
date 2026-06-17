using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using NSchema.Schema.Model;

namespace NSchema.Schema.Ddl;

/// <summary>
/// An <see cref="ISchemaProvider"/> that loads the desired schema from every NSchema SQL DDL (<c>.sql</c>) file matching a glob pattern.
/// </summary>
internal sealed class DdlSchemaProvider : ISchemaProvider
{
    private readonly string _globPattern;

    /// <param name="globPattern">A glob pattern, e.g. <c>schemas/**/*.sql</c>.</param>
    public DdlSchemaProvider(string globPattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(globPattern);
        _globPattern = globPattern;
    }

    /// <inheritdoc/>
    public async ValueTask<DatabaseSchema> GetSchema(string[]? schemaNames = null, CancellationToken cancellationToken = default)
    {
        var (root, pattern) = SplitGlobRoot(_globPattern);
        var matcher = new Matcher();
        matcher.AddInclude(pattern);

        var files = matcher
            .Execute(new DirectoryInfoWrapper(new DirectoryInfo(root)))
            .Files
            .Select(match => Path.GetFullPath(match.Path, root))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        if (files.Count == 0)
        {
            throw new FileNotFoundException($"No SQL DDL files matched the pattern \"{_globPattern}\".");
        }

        // Read the matched files concurrently, then combine in a deterministic (sorted) order so duplicate
        // detection and any reported ordering are stable regardless of how the reads interleave.
        var reads = files.Select(file => ReadFile(file, cancellationToken)).ToList();
        var combined = new DatabaseSchema();
        foreach (var read in reads)
        {
            combined = combined.Combine(await read);
        }

        return combined.Filter(schemaNames);
    }

    private static async Task<DatabaseSchema> ReadFile(string path, CancellationToken cancellationToken)
    {
        var text = await File.ReadAllTextAsync(path, cancellationToken);
        return DdlReader.Instance.Read(text).Schema;
    }

    // Splits a glob into the directory it is matched against and the pattern relative to that directory
    // (Matcher only matches relative patterns).
    private static (string Root, string Pattern) SplitGlobRoot(string globPattern)
    {
        // A wildcard-free pattern is a literal file path: match exactly that file in its directory.
        if (!globPattern.Contains('*') && !globPattern.Contains('?'))
        {
            var directory = Path.GetDirectoryName(globPattern);
            return (string.IsNullOrEmpty(directory) ? System.Environment.CurrentDirectory : directory,
                    Path.GetFileName(globPattern));
        }

        // A relative glob is rooted at the current working directory.
        if (!Path.IsPathRooted(globPattern))
        {
            return (Environment.CurrentDirectory, globPattern);
        }

        // A rooted glob is split at the first segment containing a wildcard, so its static leading path
        // becomes the matcher root.
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
