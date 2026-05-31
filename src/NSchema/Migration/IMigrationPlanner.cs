using NSchema.Migration.Plan;
using NSchema.Schema;

namespace NSchema.Migration;

/// <summary>
/// Builds a <see cref="MigrationPlan"/> by diffing the desired schema against the current database state.
/// </summary>
internal interface IMigrationPlanner
{
    /// <summary>
    /// Generates a migration plan that outlines the necessary steps to migrate the database from its current state
    /// to the desired state.
    /// </summary>
    /// <param name="currentSchema">The current database schema to diff against.</param>
    /// <param name="desiredSchema">The desired database schema to diff towards.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The generated migration plan and any non-fatal diagnostics.</returns>
    Task<MigrationPlanResult> Plan(
        DatabaseSchema currentSchema,
        DatabaseSchema desiredSchema,
        CancellationToken cancellationToken = default
    );
}
