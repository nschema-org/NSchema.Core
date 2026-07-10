using NSchema.Diff.Model;
using NSchema.Plan.Model;
using NSchema.Sql.Model;

namespace NSchema.Plan;

/// <summary>
/// The migration planner's output.
/// </summary>
/// <param name="Plan">The computed migration plan: ordered actions plus the pre/post deployment scripts.</param>
/// <param name="Diff">The structured diff the plan was derived from.</param>
internal sealed record PlannedMigration(DatabaseDiff Diff, MigrationPlan Plan)
{
    /// <summary>
    /// The scripts the plan will execute.
    /// </summary>
    public IReadOnlyList<ScriptHash> Scripts { get; init; } = [];
}
