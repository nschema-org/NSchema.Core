using NSchema.Diff.Model;
using NSchema.Plan.Model;

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
    /// <returns>The dependency-ordered migration plan.</returns>
    MigrationPlan Linearize(MigrationDiff diff);
}
