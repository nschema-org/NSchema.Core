using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Scripts;

/// <summary>
/// <c>ON PRE|POST DEPLOYMENT</c>.
/// </summary>
/// <param name="Phase">The deployment phase.</param>
public sealed record DeploymentEventClause(DeploymentPhase Phase) : ScriptEventClause
{
    /// <summary>
    /// The <c>PRE</c>/<c>POST</c> keyword token, when parsed.
    /// </summary>
    public Token? PhaseKeyword { get; init; }

    /// <summary>
    /// The <c>DEPLOYMENT</c> keyword token, when parsed.
    /// </summary>
    public Token? DeploymentKeyword { get; init; }

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            if (PhaseKeyword is { } phase)
            {
                yield return phase;
            }
            if (DeploymentKeyword is { } deployment)
            {
                yield return deployment;
            }
        }
    }
}
