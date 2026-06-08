namespace NSchema.Operations.Plan;

/// <summary>
/// Arguments for a <see cref="IPlanOperation"/> run.
/// </summary>
public sealed record PlanArguments
{
    /// <summary>
    /// The schemas to scope the plan to. When <see langword="null"/>, scope is derived from the desired schema.
    /// </summary>
    public string[]? Schemas { get; init; }
}
