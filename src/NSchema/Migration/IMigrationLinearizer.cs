using NSchema.Migration.Diff.Model;
using NSchema.Migration.Plan;
using NSchema.Schema;

namespace NSchema.Migration;

/// <summary>
/// Linearizes a structured <see cref="MigrationDiff"/> into an executable <see cref="MigrationPlan"/>: a flat,
/// dependency-ordered list of <see cref="MigrationAction"/>s. This is the inverse projection of the comparer —
/// the diff is the semantic model, the plan is the execution schedule derived from it.
/// </summary>
public interface IMigrationLinearizer
{
    /// <summary>
    /// Produces the ordered migration plan that realizes the given diff.
    /// </summary>
    /// <param name="diff">The structured diff to linearize.</param>
    /// <param name="desiredSchema">The desired schema the plan targets, carried through onto <see cref="MigrationPlan.DesiredSchema"/>.</param>
    /// <returns>The dependency-ordered migration plan.</returns>
    MigrationPlan Linearize(MigrationDiff diff, DatabaseSchema desiredSchema);
}
