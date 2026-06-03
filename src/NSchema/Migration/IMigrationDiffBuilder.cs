using NSchema.Migration.Diff;
using NSchema.Migration.Plan;

namespace NSchema.Migration;

/// <summary>
/// Rearranges a flat <see cref="MigrationPlan"/> into a structured, hierarchical <see cref="MigrationDiff"/>.
/// </summary>
public interface IMigrationDiffBuilder
{
    /// <summary>
    /// Builds a structured diff from the given plan.
    /// </summary>
    /// <param name="plan">The plan to rearrange.</param>
    /// <returns>The hierarchical diff model.</returns>
    MigrationDiff Build(MigrationPlan plan);
}
