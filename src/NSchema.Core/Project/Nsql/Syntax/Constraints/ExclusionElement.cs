using NSchema.Model;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Constraints;

/// <summary>
/// A single element of an exclusion constraint: a column name or a raw expression, with its operator.
/// </summary>
/// <param name="Operator">The exclusion operator (e.g. <c>=</c>, <c>&amp;&amp;</c>).</param>
/// <param name="Column">The column name; exactly one of <paramref name="Column"/> and <paramref name="Expression"/> is given.</param>
/// <param name="Expression">The raw element expression; exactly one of <paramref name="Column"/> and <paramref name="Expression"/> is given.</param>
public sealed record ExclusionElement(string Operator, Identifier? Column = null, SqlText? Expression = null) : NsqlNode
{
    /// <summary>
    /// The <c>(</c> token, when parsed with an expression element.
    /// </summary>
    public Token? OpenParenToken { get; init; }

    /// <summary>
    /// The verbatim expression span token, when parsed with an expression element.
    /// </summary>
    public Token? ExpressionToken { get; init; }

    /// <summary>
    /// The <c>)</c> token, when parsed with an expression element.
    /// </summary>
    public Token? CloseParenToken { get; init; }

    /// <summary>
    /// The verbatim <c>WITH operator</c> span token, when parsed.
    /// </summary>
    public Token? WithOperatorToken { get; init; }

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            if (Column is { } column)
            {
                yield return column;
            }
            if (OpenParenToken is { } open)
            {
                yield return open;
            }
            if (ExpressionToken is { } expression)
            {
                yield return expression;
            }
            if (CloseParenToken is { } close)
            {
                yield return close;
            }
            if (WithOperatorToken is { } with)
            {
                yield return with;
            }
        }
    }
}
