namespace NSchema.Plugins;

/// <summary>
/// The contract common to every NSchema plugin.
/// </summary>
/// <remarks>
/// Implement one of the derived interfaces, never this one directly.
/// </remarks>
public interface INSchemaPlugin
{
    /// <summary>
    /// The configuration-block label this plugin serves.
    /// </summary>
    string Label { get; }

    /// <summary>
    /// Builds the starter configuration block this plugin contributes when a new project is scaffolded.
    /// </summary>
    /// <param name="context">Describes what is being scaffolded (e.g. the target environment).</param>
    string GetScaffoldTemplate(ScaffoldContext context);
}
