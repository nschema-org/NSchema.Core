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
    /// The <c>CREATE</c> keyword token.
    /// </summary>
    public Token CreateKeyword { get; init; } = Token.Keyword(NsqlKeywords.Create);

    /// <summary>
    /// The <c>FUNCTION</c>/<c>PROCEDURE</c> keyword token, when parsed.
    /// </summary>
    public Token? KindKeyword { get; init; }

    /// <summary>
    /// The <c>(</c> token opening the arguments.
    /// </summary>
    public Token OpenParenToken { get; init; } = Token.Punctuation(TokenKind.LeftParen, NsqlSymbols.LeftParen);

    /// <summary>
    /// The verbatim argument-list span token, when parsed.
    /// </summary>
    public Token? ArgumentsToken { get; init; }

    /// <summary>
    /// The <c>)</c> token closing the arguments.
    /// </summary>
    public Token CloseParenToken { get; init; } = Token.Punctuation(TokenKind.RightParen, NsqlSymbols.RightParen);

    /// <summary>
    /// The verbatim definition span token, when parsed.
    /// </summary>
    public Token? DefinitionToken { get; init; }

    /// <summary>
    /// The terminating <c>;</c> token.
    /// </summary>
    public Token SemicolonToken { get; init; } = Token.Punctuation(TokenKind.Semicolon, NsqlSymbols.Semicolon);

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            if (DocComment is { } doc)
            {
                yield return doc;
            }
            yield return CreateKeyword;
            yield return KindKeyword ?? Token.Keyword(Kind == RoutineKind.Procedure ? NsqlKeywords.Procedure : NsqlKeywords.Function);
            yield return Name;
            yield return OpenParenToken;
            if (ArgumentsToken is { } arguments)
            {
                yield return arguments;
            }
            yield return CloseParenToken;
            if (DefinitionToken is { } definition)
            {
                yield return definition;
            }
            yield return SemicolonToken;
        }
    }
}
