using NSchema.Diff.Model.Services;
using NSchema.Model;
using NSchema.Project.Model.Directives;

namespace NSchema.Plan.Model.Services;

/// <summary>
/// Builds a <see cref="MigrationPlan"/> by diffing the desired schema against the current database state.
/// </summary>
internal interface IMigrationPlanner
{
    /// <summary>
    /// Validates the declared project against the registered project policies.
    /// </summary>
    /// <param name="desired">The declared project to validate (already aggregated and expanded).</param>
    /// <returns>The project-policy findings; the caller decides how to surface any errors.</returns>
    Result Validate(ProjectDefinition desired);

    /// <summary>
    /// Builds the complete executable plan migrating the database from its current state to the desired state.
    /// </summary>
    /// <param name="current">What currently exists.</param>
    /// <param name="desired">The desired project to plan towards.</param>
    /// <param name="scope">The objects that are covered by this plan.</param>
    /// <returns>The plan and every finding produced.</returns>
    Result<MigrationPlan> Plan(CurrentState current, ProjectDefinition desired, PlanningScope scope);
}
