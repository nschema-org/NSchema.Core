namespace NSchema.Plugins;

/// <summary>
/// Describes the context in which a plugin's scaffold template is requested.
/// </summary>
public sealed record ScaffoldContext
{
    /// <summary>
    /// The environment the fragment is being scaffolded for, or <see langword="null"/> for the base configuration.
    /// </summary>
    public string? EnvironmentName { get; init; }
}
