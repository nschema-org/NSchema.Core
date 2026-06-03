using NSchema.Plan.Model;
using NSchema.Schema.Model;
using NSchema.Scripts.Model;

namespace NSchema.Migration;

/// <summary>
/// Builds a <see cref="MigrationPlan"/> by diffing the desired schema against the current database state.
/// </summary>
public interface IMigrationPlanner
{
    /// <summary>
    /// Generates a migration plan that outlines the necessary steps to migrate the database from its current state to the desired state.
    /// </summary>
    /// <param name="currentSchema">The current database schema to diff against.</param>
    /// <param name="desiredSchema">The desired database schema to diff towards (already aggregated and transformed).</param>
    /// <param name="scripts">The pre- and post-deployment scripts to fold into the diff and splice into the plan.</param>
    /// <returns>The generated migration plan and any non-fatal diagnostics.</returns>
    MigrationPlanResult Plan(DatabaseSchema currentSchema, DatabaseSchema desiredSchema, IReadOnlyList<Script> scripts);
}
