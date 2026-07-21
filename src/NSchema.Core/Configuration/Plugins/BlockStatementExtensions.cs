using NSchema.Project.Nsql.Syntax.Blocks;

namespace NSchema.Configuration.Plugins;

/// <summary>
/// Adapts a parsed <see cref="BlockStatement"/> to the <see cref="PluginSettings"/> its label and attributes describe.
/// </summary>
internal static class BlockStatementExtensions
{
    /// <summary>
    /// The statement as a <see cref="PluginSettings"/> — its label plus its attributes as a flat, case-insensitive map.
    /// </summary>
    public static PluginSettings ToSettings(this BlockStatement statement) =>
        new(statement.Label?.Value, statement.Attributes.ToDictionary(a => a.Key, string? (a) => a.Value, StringComparer.OrdinalIgnoreCase));
}
