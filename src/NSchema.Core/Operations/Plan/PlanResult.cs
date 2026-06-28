using NSchema.Diff.Model;
using NSchema.Plan.Model;
using NSchema.Sql.Model;

namespace NSchema.Operations.Plan;

/// <summary>
/// The result of a plan (or teardown plan).
/// </summary>
/// <param name="Diff">The structured diff the plan was derived from.</param>
/// <param name="Plan">The migration plan: ordered actions plus the pre/post deployment scripts.</param>
/// <param name="Sql">The generated SQL plan.</param>
public sealed record PlanResult(DatabaseDiff? Diff, MigrationPlan? Plan, SqlPlan? Sql)
{
    /// <summary>
    /// Whether the plan contains any changes (a non-empty diff).
    /// </summary>
    public bool HasChanges => Diff is { IsEmpty: false };
}
