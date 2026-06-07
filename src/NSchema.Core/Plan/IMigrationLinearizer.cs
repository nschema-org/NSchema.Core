using NSchema.Diff.Model;
using NSchema.Plan.Model;

namespace NSchema.Plan;

/// <summary>
/// Linearizes a structured <see cref="DatabaseDiff"/> into an executable <see cref="MigrationPlan"/>.
/// </summary>
public interface IMigrationLinearizer
{
    /// <summary>
    /// Produces the ordered migration plan that realizes the given diff.
    /// </summary>
    /// <param name="diff">The structured diff to linearize.</param>
    /// <returns>The dependency-ordered action list.</returns>
    IReadOnlyList<MigrationAction> Linearize(DatabaseDiff diff);
}
