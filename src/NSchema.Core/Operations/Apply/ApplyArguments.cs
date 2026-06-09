namespace NSchema.Operations.Apply;

/// <summary>
/// Arguments for an <see cref="IApplyOperation"/> run.
/// </summary>
public sealed record ApplyArguments
{
    /// <summary>
    /// The schemas to scope the apply to. When <see langword="null"/>, scope is derived from the desired schema.
    /// Ignored when <see cref="PlanFile"/> is set (a saved plan already fixes its scope).
    /// </summary>
    public string[]? Schemas { get; init; }

    /// <summary>
    /// When set, apply executes a saved plan file instead of computing a fresh plan.
    /// </summary>
    public string? PlanFile { get; init; }
}
