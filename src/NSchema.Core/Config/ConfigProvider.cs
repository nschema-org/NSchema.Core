using NSchema.Project.Nsql;

namespace NSchema.Config;

/// <summary>
/// The entry point from configuration files to a <see cref="ConfigDefinition"/>.
/// </summary>
public static class ConfigProvider
{
    /// <summary>
    /// Reads the configuration files at <paramref name="paths"/> and resolves them into the configuration.
    /// </summary>
    /// <param name="paths">The configuration files that make up one configuration.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public static async Task<Result<ConfigDefinition, NsqlDiagnostic>> GetConfig(IReadOnlyList<string> paths, CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<NsqlDiagnostic>();
        var documents = new List<NsqlConfigDocument>();
        foreach (var path in paths)
        {
            var read = await NsqlReader.ReadConfigFile(path, cancellationToken);
            diagnostics.AddRange(read.Diagnostics);
            if (read.Value is { } document)
            {
                documents.Add(document);
            }
        }

        var assembled = ConfigAssembler.Assemble(documents);
        diagnostics.AddRange(assembled.Diagnostics);
        return Result<ConfigDefinition, NsqlDiagnostic>.From(assembled.Value, diagnostics);
    }
}
