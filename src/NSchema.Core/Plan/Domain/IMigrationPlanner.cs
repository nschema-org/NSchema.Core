using NSchema.Diff.Domain;
using NSchema.Plan.Domain.Models;
using NSchema.Policies;
using NSchema.Project.Domain.Models;

namespace NSchema.Plan.Domain;

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
    /// Builds the complete executable plan migrating the database from its current state to the desired state.
    /// </summary>
    /// <param name="current">What currently exists.</param>
    /// <param name="desired">The desired project to plan towards.</param>
    /// <returns>The plan and every finding produced.</returns>
    Result<MigrationPlan> Plan(CurrentState current, ProjectDefinition desired);

    /// <summary>
    /// Builds a teardown plan that drops everything in <paramref name="currentSchema"/>.
    /// </summary>
    /// <param name="currentSchema">The managed schema to tear down.</param>
    /// <returns>The teardown plan, including the structured diff describing the removals.</returns>
    Result<MigrationPlan> PlanTeardown(DatabaseSchema currentSchema);
}
