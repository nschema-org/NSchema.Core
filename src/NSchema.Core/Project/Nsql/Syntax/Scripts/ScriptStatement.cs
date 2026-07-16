using NSchema.Model;

namespace NSchema.Project.Nsql.Syntax.Scripts;

/// <summary>
/// <c>SCRIPT name RUN [ALWAYS|ONCE] ON event [(options)] AS $$ body $$;</c>
/// </summary>
/// <param name="Name">The script name.</param>
/// <param name="RunCondition">The <c>RUN</c> condition, or <see langword="null"/> for the bare <c>RUN</c> of a change-event script.</param>
/// <param name="Event">The <c>ON</c> event clause.</param>
/// <param name="Body">The body text, delimiters stripped.</param>
/// <param name="RunOutsideTransaction">Whether <c>run_outside_transaction = true</c> is written.</param>
public sealed record ScriptStatement(
    Identifier Name,
    RunCondition? RunCondition,
    ScriptEventClause Event,
    SqlText Body,
    bool RunOutsideTransaction = false
) : NsqlStatement
{
    /// <summary>
    /// The <c>RUN</c> condition — only valid on a deployment event, since a change-event script runs whenever
    /// its change is planned rather than on a schedule.
    /// </summary>
    public RunCondition? RunCondition { get; } = Validate(RunCondition, Event);

    private static RunCondition? Validate(RunCondition? condition, ScriptEventClause @event) =>
        condition is null || @event is DeploymentEventClause
            ? condition
            : throw new NsqlSyntaxException(
                "A run condition (ALWAYS or ONCE) is only valid on a deployment event; a change-event script runs whenever its change is planned.",
                @event.Position);
}
