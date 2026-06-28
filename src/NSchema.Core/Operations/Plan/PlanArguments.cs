namespace NSchema.Operations.Plan;

/// <summary>
/// Arguments for an operation run.
/// </summary>
public sealed record PlanArguments
{
    /// <summary>
    /// The schemas to scope the plan to. When <see langword="null"/>, scope is derived from the desired schema.
    /// </summary>
    public string[]? Schemas { get; init; }

    /// <summary>
    /// When set, the computed plan is written to this file path so it can be applied later.
    /// </summary>
    public string? OutFile { get; init; }
}
