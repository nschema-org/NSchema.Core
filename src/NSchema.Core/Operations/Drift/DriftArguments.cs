namespace NSchema.Operations.Drift;

/// <summary>
/// Arguments for an <see cref="IDriftOperation"/> run.
/// </summary>
public sealed record DriftArguments
{
    /// <summary>
    /// The schemas to scope the drift check to. When <see langword="null"/>, the whole recorded state is checked.
    /// </summary>
    public string[]? Schemas { get; init; }
}
