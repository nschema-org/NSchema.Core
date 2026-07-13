using NSchema.Project.Domain.Models;

namespace NSchema.Project.Nsql.Syntax.Triggers;

/// <summary>
/// <c>AS $$ body $$</c> — an inline dollar-quoted trigger body.
/// </summary>
/// <param name="Body">The body text, delimiters stripped.</param>
public sealed record InlineBodyAction(SqlText Body) : TriggerAction;