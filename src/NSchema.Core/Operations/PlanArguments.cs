using NSchema.Project.Domain.Models;

namespace NSchema.Operations;

/// <summary>
/// Arguments for computing a migration plan.
/// </summary>
public sealed record PlanArguments
{
    /// <summary>
    /// The schemas to scope the plan to. When <see langword="null"/>, scope is derived from the desired schema.
    /// Ignored for a <see cref="PlanTarget.Teardown"/>, which is unscoped.
    /// </summary>
    public SchemaScope Scope { get; init; } = SchemaScope.All;

    /// <summary>
    /// When set, the computed plan is written to this file path so it can be applied later.
    /// </summary>
    public string? OutFile { get; init; }

    /// <summary>
    /// What to plan. Defaults to <see cref="PlanTarget.Recorded"/> (a preview); an apply uses
    /// <see cref="PlanTarget.Live"/>, and a teardown uses <see cref="PlanTarget.Teardown"/>.
    /// </summary>
    public PlanTarget Target { get; init; } = PlanTarget.Recorded;
}
