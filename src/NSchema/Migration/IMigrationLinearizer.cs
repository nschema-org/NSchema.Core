using NSchema.Diff.Model;
using NSchema.Plan.Model;
using NSchema.Schema.Model;

namespace NSchema.Migration;

/// <summary>
/// Linearizes a structured <see cref="MigrationDiff"/> into an executable <see cref="MigrationPlan"/>.
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
