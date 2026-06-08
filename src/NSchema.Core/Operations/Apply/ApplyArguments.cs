namespace NSchema.Operations.Apply;

/// <summary>
/// Arguments for an <see cref="IApplyOperation"/> run.
/// </summary>
public sealed record ApplyArguments
{
    /// <summary>
    /// The schemas to scope the apply to. When <see langword="null"/>, scope is derived from the desired schema.
    /// </summary>
    public string[]? Schemas { get; init; }
}
