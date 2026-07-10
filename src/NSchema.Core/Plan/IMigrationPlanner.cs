using NSchema.Diagnostics;
using NSchema.Plan.Model;
using NSchema.Policies;
using NSchema.Schema.Model;

namespace NSchema.Plan;

/// <summary>
/// Builds a <see cref="MigrationPlan"/> by diffing the desired schema against the current database state.
/// </summary>
internal interface IMigrationPlanner
{
    /// <summary>
    /// Runs the schema stage in isolation: validates the desired schema against the registered schema policies.
    /// </summary>
    /// <param name="desiredSchema">The desired schema to validate (already aggregated and transformed).</param>
    /// <returns>The schema-policy diagnostics; the caller decides how to surface any errors.</returns>
    PolicyDiagnostics Validate(DatabaseSchema desiredSchema);

    /// <summary>
    /// Generates a migration plan that outlines the necessary steps to migrate the database from its current state to the desired state.
    /// </summary>
    /// <param name="current">What currently exists.</param>
    /// <param name="desired">The desired project to plan towards.</param>
    /// <returns>The generated migration plan and any non-fatal diagnostics.</returns>
    Result<PlannedMigration> Plan(CurrentState current, DesiredProject desired);

    /// <summary>
    /// Builds a teardown plan that drops everything in <paramref name="currentSchema"/>.
    /// </summary>
    /// <param name="currentSchema">The managed schema to tear down.</param>
    /// <returns>The teardown plan and the structured diff describing the removals.</returns>
    Result<PlannedMigration> PlanTeardown(DatabaseSchema currentSchema);
}
