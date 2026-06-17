using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using NSchema.Scripts.Model;

namespace NSchema.Scripts;

/// <summary>
/// An <see cref="IScriptProvider"/> that loads deployment scripts from every SQL file matching a glob pattern.
/// </summary>
/// <remarks>
/// The glob is evaluated each time scripts are read, so the script set reflects the filesystem at plan/apply time —
/// the same execution-time resolution the desired schema uses.
/// </remarks>
internal sealed class ScriptProvider : IScriptProvider
{
    private readonly ScriptType _type;
    private readonly string _baseDirectory;
    private readonly Matcher _matcher;

    /// <param name="type">When the matched scripts run relative to the main migration actions.</param>
    /// <param name="baseDirectory">The directory the matcher is run against.</param>
    /// <param name="matcher">The glob matcher selecting files (relative to <paramref name="baseDirectory"/>); may carry excludes.</param>
    public ScriptProvider(ScriptType type, string baseDirectory, Matcher matcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        ArgumentNullException.ThrowIfNull(matcher);
        _type = type;
        _baseDirectory = baseDirectory;
        _matcher = matcher;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<Script>> GetScripts(CancellationToken cancellationToken = default)
    {
        var files = _matcher
            .Execute(new DirectoryInfoWrapper(new DirectoryInfo(_baseDirectory)))
            .Files
            .Select(match => Path.GetFullPath(match.Path, _baseDirectory))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        var reads = files.Select(file => ReadScript(file, cancellationToken));

        // Task.WhenAll preserves the order of its inputs, so the sorted file order carries through to the result.
        return await Task.WhenAll(reads);
    }

    private async Task<Script> ReadScript(string path, CancellationToken cancellationToken)
    {
        var sql = await File.ReadAllTextAsync(path, cancellationToken);
        return new Script(Path.GetFileName(path), sql, _type);
    }
}
