using System.Text;
using NSchema.Plugins.Model;
using NSchema.Plugins.Model.LockFiles;
using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax.Config;
using NSchema.Project.Nsql.Syntax.Lock;

namespace NSchema.Plugins;

/// <summary>
/// Reads and writes the plugin lockfile (<c>nschema.lock</c>). Static rather than a DI service: the lockfile is
/// a single file at a caller-supplied path, with no per-application state to hold. Core owns the format both
/// ways, so a written lockfile always round-trips.
/// </summary>
public static class LockFileManager
{
    private const string Source = "lockfile";
    private const string SourceAttribute = "source";
    private const string VersionAttribute = "version";

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
            foreach (var statement in value.Statements)
            {
                if (FromStatement(statement, diagnostics) is { } plugin)
                {
                    plugins.Add(plugin);
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

    private static LockedPlugin? FromStatement(LockStatement statement, List<Diagnostic> diagnostics)
    {
        var source = StringAttribute(statement, SourceAttribute, diagnostics);
        var version = StringAttribute(statement, VersionAttribute, diagnostics);
        if (source is null || version is null)
        {
            return null;
        }

        if (!PackageId.IsValid(source))
        {
            diagnostics.Add(Diagnostic.Error(Source, $"'{source}' is not a valid package id."));
            return null;
        }

        if (!SemanticVersion.TryParse(version, out var semanticVersion))
        {
            diagnostics.Add(Diagnostic.Error(Source, $"'{version}' is not a valid version."));
            return null;
        }

        return new LockedPlugin(new PackageId(source), semanticVersion);
    }

    private static string? StringAttribute(LockStatement statement, string key, List<Diagnostic> diagnostics)
    {
        var attribute = statement.Attributes.FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase));
        if (attribute is null)
        {
            diagnostics.Add(Diagnostic.Error(Source, $"A LOCK statement requires a '{key}' attribute."));
            return null;
        }

        if (attribute.Value is not StringValue value)
        {
            diagnostics.Add(Diagnostic.Error(Source, $"The '{key}' attribute must be a string."));
            return null;
        }

        return value.Value;
    }
}
