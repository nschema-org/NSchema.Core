namespace NSchema.Operations.PlanDestroy;

/// <summary>
/// Arguments for an <see cref="IPlanDestroyOperation"/> run.
/// </summary>
public sealed record PlanDestroyArguments
{
    /// <summary>
    /// When set, the computed teardown plan is written to this file path so it can be applied later.
    /// </summary>
    public string? OutFile { get; init; }
}
