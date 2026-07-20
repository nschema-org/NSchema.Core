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
    /// Builds the starter configuration block this plugin contributes when a new project is scaffolded.
    /// </summary>
    /// <param name="context">Describes what is being scaffolded (e.g. the target environment).</param>
    /// <remarks>
    /// The template is source text, not a model, so it can carry comments.
    /// The host formats it, so canonical styling is applied without losing them.
    /// </remarks>
    string GetScaffoldTemplate(ScaffoldContext context);
}
