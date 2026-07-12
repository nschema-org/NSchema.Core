namespace NSchema.Project.Domain.Models.Scripts;

/// <summary>
/// A deployment bookend: the script runs on every apply, before or after the migration actions.
/// </summary>
/// <param name="Phase">Where the script runs relative to the migration actions.</param>
public sealed record DeploymentEvent(DeploymentPhase Phase) : ScriptEvent
{
    /// <inheritdoc />
    public override string Description => Phase == DeploymentPhase.Pre ? "PRE DEPLOYMENT" : "POST DEPLOYMENT";
}
