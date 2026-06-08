namespace NSchema.Operations.Show;

/// <summary>
/// Arguments for an <see cref="IShowOperation"/> run.
/// </summary>
public sealed record ShowArguments
{
    /// <summary>
    /// The schemas to scope the output to. When <see langword="null"/>, the whole recorded state is shown.
    /// </summary>
    public string[]? Schemas { get; init; }
}
