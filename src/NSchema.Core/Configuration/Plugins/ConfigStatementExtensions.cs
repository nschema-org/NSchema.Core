using NSchema.Project.Nsql.Syntax.Config;

namespace NSchema.Configuration.Plugins;

/// <summary>
/// Adapts a parsed <see cref="ConfigStatement"/> to the <see cref="PluginConfig"/> its label and attributes describe.
/// </summary>
internal static class ConfigStatementExtensions
{
    /// <summary>
    /// The statement as a <see cref="PluginConfig"/> — its label plus its attributes as a flat, case-insensitive map.
    /// </summary>
    public static PluginConfig ToConfig(this ConfigStatement statement) =>
        new(statement.Label?.Value, statement.Attributes.ToDictionary(a => a.Key, string? (a) => a.Value, StringComparer.OrdinalIgnoreCase));
}
