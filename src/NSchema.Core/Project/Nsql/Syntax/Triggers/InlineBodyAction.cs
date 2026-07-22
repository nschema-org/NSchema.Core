using NSchema.Model;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Triggers;

/// <summary>
/// <c>AS $$ body $$</c> — an inline dollar-quoted trigger body.
/// </summary>
/// <param name="Body">The body text, delimiters stripped.</param>
public sealed record InlineBodyAction(SqlText Body) : TriggerAction
{
    /// <summary>
    /// The <c>AS</c> keyword token.
    /// </summary>
    public Token AsKeyword { get; init; } = Token.Keyword(NsqlKeywords.As);

    /// <summary>
    /// The dollar-quoted body token, when parsed.
    /// </summary>
    public Token? BodyToken { get; init; }

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            yield return AsKeyword;
            if (BodyToken is { } body)
            {
                yield return body;
            }
        }
    }
}
