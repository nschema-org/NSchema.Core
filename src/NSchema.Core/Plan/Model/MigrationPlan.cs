using NSchema.Schema.Model;

namespace NSchema.Plan.Model;

/// <summary>
/// Represents a migration plan that outlines the necessary steps to migrate a database schema from its current state to a desired target state.
/// </summary>
/// <param name="Actions">The ordered list of migration actions that need to be executed to apply the changes described in the migration plan.</param>
/// <param name="PreDeploymentScripts">The pre-deployment scripts to run before the migration actions.</param>
/// <param name="PostDeploymentScripts">The post-deployment scripts to run after the migration actions.</param>
public sealed record MigrationPlan(
    IReadOnlyList<MigrationAction> Actions,
    IReadOnlyList<Script> PreDeploymentScripts,
    IReadOnlyList<Script> PostDeploymentScripts
);
