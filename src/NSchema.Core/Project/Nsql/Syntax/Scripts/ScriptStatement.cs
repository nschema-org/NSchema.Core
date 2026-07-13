using NSchema.Project.Domain.Models;

namespace NSchema.Project.Nsql.Syntax.Scripts;

/// <summary>
/// <c>SCRIPT 'name' RUN [ALWAYS|ONCE] ON event [(options)] AS $$ body $$;</c>
/// </summary>
/// <param name="Name">The script name as written (inside a template body it may carry the <c>{schema}</c> pattern).</param>
/// <param name="RunCondition">The <c>RUN</c> condition (default <see cref="RunCondition.Always"/>).</param>
/// <param name="Event">The <c>ON</c> event clause.</param>
/// <param name="Body">The body text, delimiters stripped.</param>
/// <param name="RunOutsideTransaction">Whether <c>run_outside_transaction = true</c> is written.</param>
public sealed record ScriptStatement(
    string Name,
    RunCondition RunCondition,
    ScriptEventClause Event,
    SqlText Body,
    bool RunOutsideTransaction = false
) : NsqlStatement;