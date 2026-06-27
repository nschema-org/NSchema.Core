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

    /// <summary>
    /// The plugin package version to pin in the rendered block, or <see langword="null"/> when the host has not resolved one.
    /// </summary>
    public string? Version { get; init; }
}
