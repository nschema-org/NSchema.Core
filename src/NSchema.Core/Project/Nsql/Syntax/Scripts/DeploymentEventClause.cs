namespace NSchema.Project.Nsql.Syntax.Scripts;

/// <summary>
/// <c>ON PRE|POST DEPLOYMENT</c>.
/// </summary>
/// <param name="Phase">The deployment phase.</param>
public sealed record DeploymentEventClause(DeploymentPhase Phase) : ScriptEventClause;