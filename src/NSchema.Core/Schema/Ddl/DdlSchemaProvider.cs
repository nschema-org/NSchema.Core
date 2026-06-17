using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using NSchema.Schema.Model;

namespace NSchema.Schema.Ddl;

/// <summary>
/// An <see cref="ISchemaProvider"/> that loads the desired schema from every NSchema SQL DDL (<c>.sql</c>) file matching a glob pattern.
/// </summary>
internal sealed class DdlSchemaProvider : ISchemaProvider
{
    private readonly string _baseDirectory;
    private readonly string _globPattern;

    /// <param name="baseDirectory">The directory the glob is matched against.</param>
    /// <param name="globPattern">A glob pattern relative to <paramref name="baseDirectory"/>, e.g. <c>**/*.sql</c>.</param>
    public DdlSchemaProvider(string baseDirectory, string globPattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(globPattern);
        _baseDirectory = baseDirectory;
        _globPattern = globPattern;
    }

    /// <inheritdoc/>
    public async ValueTask<DatabaseSchema> GetSchema(string[]? schemaNames = null, CancellationToken cancellationToken = default)
    {
        var matcher = new Matcher();
        matcher.AddInclude(_globPattern);

        var files = matcher
            .Execute(new DirectoryInfoWrapper(new DirectoryInfo(_baseDirectory)))
            .Files
            .Select(match => Path.GetFullPath(match.Path, _baseDirectory))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        if (files.Count == 0)
        {
            throw new FileNotFoundException($"No SQL DDL files matched \"{_globPattern}\" under \"{_baseDirectory}\".");
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
}
