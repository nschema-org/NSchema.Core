using NSchema.Model;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Routines;

/// <summary>
/// <c>CREATE FUNCTION|PROCEDURE schema.name [RENAMED FROM old] (arguments) definition;</c>
/// </summary>
/// <param name="Name">The routine name as written.</param>
/// <param name="Kind">Whether the statement declares a function or a procedure.</param>
/// <param name="Arguments">The argument list, verbatim (the text inside the parentheses; may be empty).</param>
/// <param name="Definition">Everything after the argument list, verbatim.</param>
public sealed record CreateRoutineStatement(
    QualifiedName Name,
    RoutineKind Kind,
    SqlText Arguments,
    SqlText Definition
) : NsqlStatement
{
    /// <summary>
    /// The <c>CREATE</c> keyword token, when parsed.
    /// </summary>
    public Token? CreateKeyword { get; init; }

    /// <summary>
    /// The <c>FUNCTION</c>/<c>PROCEDURE</c> keyword token, when parsed.
    /// </summary>
    public Token? KindKeyword { get; init; }

    /// <summary>
    /// The <c>(</c> token opening the arguments, when parsed.
    /// </summary>
    public Token? OpenParenToken { get; init; }

    /// <summary>
    /// The verbatim argument-list span token, when parsed.
    /// </summary>
    public Token? ArgumentsToken { get; init; }

    /// <summary>
    /// The <c>)</c> token closing the arguments, when parsed.
    /// </summary>
    public Token? CloseParenToken { get; init; }

    /// <summary>
    /// The verbatim definition span token, when parsed.
    /// </summary>
    public Token? DefinitionToken { get; init; }

    /// <summary>
    /// The terminating <c>;</c> token, when parsed.
    /// </summary>
    public Token? SemicolonToken { get; init; }

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            if (DocComment is { } doc)
            {
                yield return doc;
            }
            if (CreateKeyword is { } create)
            {
                yield return create;
            }
            if (KindKeyword is { } kind)
            {
                yield return kind;
            }
            yield return Name;
            if (OpenParenToken is { } open)
            {
                yield return open;
            }
            if (ArgumentsToken is { } arguments)
            {
                yield return arguments;
            }
            if (CloseParenToken is { } close)
            {
                yield return close;
            }
            if (DefinitionToken is { } definition)
            {
                yield return definition;
            }
            if (SemicolonToken is { } semicolon)
            {
                yield return semicolon;
            }
        }
    }
}
