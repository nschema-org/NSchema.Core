using NSchema.Model;
using NSchema.Project.Nsql.Syntax.Tables;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Constraints;

/// <summary>
/// <c>CONSTRAINT name CHECK (expression)</c> — in a table body or a domain declaration.
/// </summary>
/// <param name="Name">The constraint name.</param>
/// <param name="Expression">The boolean expression the constraint enforces.</param>
public sealed record CheckDefinition(Identifier Name, SqlText Expression) : TableMember
{
    /// <summary>
    /// The <c>CONSTRAINT</c> keyword token, when parsed as a table member.
    /// </summary>
    public Token? ConstraintKeyword { get; init; }

    /// <summary>
    /// The <c>CHECK</c> keyword token, when parsed as a table member.
    /// </summary>
    public Token? CheckKeyword { get; init; }

    /// <summary>
    /// The <c>(</c> token, when parsed as a table member.
    /// </summary>
    public Token? OpenParenToken { get; init; }

    /// <summary>
    /// The verbatim check-expression span token, when parsed as a table member.
    /// </summary>
    public Token? ExpressionToken { get; init; }

    /// <summary>
    /// The <c>)</c> token, when parsed as a table member.
    /// </summary>
    public Token? CloseParenToken { get; init; }

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            if (DocComment is { } doc)
            {
                yield return doc;
            }
            if (ConstraintKeyword is { } constraint)
            {
                yield return constraint;
            }
            yield return Name;
            if (CheckKeyword is { } check)
            {
                yield return check;
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
        }
    }
}
