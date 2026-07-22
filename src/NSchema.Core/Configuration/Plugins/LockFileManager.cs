using System.Text;
using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax.Blocks;

namespace NSchema.Configuration.Plugins;

/// <summary>
/// Reads and writes the plugin lockfile (<c>nschema.lock</c>). Static rather than a DI service: the lockfile is
/// a single file at a caller-supplied path, with no per-application state to hold. Core owns the format both
/// ways, so a written lockfile always round-trips.
/// </summary>
public static class LockFileManager
{
    private const string Source = "lockfile";

    private const string Header = "-- nschema.lock — managed by NSchema. Do not edit by hand; regenerate it instead.";

    /// <summary>
    /// Reads the lockfile at <paramref name="path"/>. A missing file is an empty lockfile (nothing locked yet),
    /// not an error; unknown attributes are ignored, so a lockfile written by a newer NSchema still loads.
    /// </summary>
    /// <param name="path">The lockfile path.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public static async Task<Result<LockFile>> Read(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return Result.Success(LockFile.Empty);
        }

        var document = await NsqlReader.ReadLockFile(path, cancellationToken);
        var diagnostics = new List<Diagnostic>(document.Diagnostics);
        var plugins = new List<LockedPlugin>();

        if (document.Value is { } value)
        {
            foreach (var statement in value.Statements.OfType<BlockStatement>())
            {
                var result = statement.ToSettings().Get<LockedPlugin>(ignoreUnknown: true);
                diagnostics.AddRange(result.Diagnostics);

                if (result.IsSuccess)
                {
                    plugins.Add(result.Require());
                }
            }
        }

        return Result.From(new LockFile(plugins), diagnostics);
    }

    /// <summary>
    /// Writes <paramref name="lockFile"/> to <paramref name="path"/> as canonical lockfile source — one
    /// <c>LOCK</c> statement per pin, in order.
    /// </summary>
    /// <param name="path">The lockfile path.</param>
    /// <param name="lockFile">The lockfile to write.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public static async Task<Result> Write(string path, LockFile lockFile, CancellationToken cancellationToken = default)
    {
        var builder = new StringBuilder().Append(Header).Append('\n');
        foreach (var plugin in lockFile.Plugins)
        {
            builder.Append($"LOCK ( source = '{plugin.Source}', version = '{plugin.Version}' );").Append('\n');
        }

        try
        {
            await File.WriteAllTextAsync(path, builder.ToString(), cancellationToken);
            return Result.Success();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result.From(Diagnostic.Error(Source, $"Could not write the lockfile '{path}': {exception.Message:text}"));
        }
    }
}
