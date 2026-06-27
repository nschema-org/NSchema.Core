using NSchema.Diff.Model;

namespace NSchema.Operations.Plan;

/// <summary>
/// The result of a plan (or teardown plan): the structured diff it was derived from, and whether it contains changes.
/// </summary>
/// <param name="Diff">The structured diff the plan was derived from.</param>
public sealed record PlanResult(DatabaseDiff Diff)
{
    /// <summary>
    /// Whether the plan contains any changes (a non-empty diff).
    /// </summary>
    public bool HasChanges => !Diff.IsEmpty;
}
