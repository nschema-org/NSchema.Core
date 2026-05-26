using NSchema.Schema;

namespace NSchema.Migration.ScriptProviders;

/// <summary>
/// Provides migration scripts by loading a single SQL script from a specified file path.
/// </summary>
/// <param name="type">The type of the script, indicating when it should be executed in relation to the main migration actions.</param>
/// <param name="path">The file path of the SQL script to be loaded as a migration script.</param>
/// <param name="name">An optional name to assign to the script; if not provided, a name will be derived from the file name without extension.</param>
internal sealed class FileScriptProvider(ScriptType type, string path, string? name = null) : IScriptProvider
{
    public async Task<IReadOnlyList<Script>> GetScripts(CancellationToken cancellationToken = default)
    {
        var sql = await File.ReadAllTextAsync(path, cancellationToken);
        return [new Script(name ?? Path.GetFileNameWithoutExtension(path), sql, type)];
    }
}
