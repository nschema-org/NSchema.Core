using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax;

/// <summary>
/// A type reference as written: an optionally schema-qualified type name plus its optional
/// parenthesised arguments (e.g. <c>varchar(100)</c>, <c>numeric(10,2)</c>, <c>app.status</c>).
/// </summary>
/// <param name="Schema">The schema qualifier for a user-defined type, or <see langword="null"/>.</param>
/// <param name="Name">The type name.</param>
/// <param name="Arguments">The text inside the parentheses (e.g. <c>100</c> or <c>10,2</c>), or <see langword="null"/>.</param>
public sealed record TypeName(Identifier? Schema, Identifier Name, string? Arguments = null) : NsqlNode
{
    /// <summary>
    /// The <c>.</c> token after the schema qualifier, when parsed qualified.
    /// </summary>
    public Token? SchemaDotToken { get; init; }

    /// <summary>
    /// The <c>(</c> token opening the arguments, when parsed with arguments.
    /// </summary>
    public Token? OpenParenToken { get; init; }

    /// <summary>
    /// The precision (first) argument token, when parsed with arguments.
    /// </summary>
    public Token? PrecisionToken { get; init; }

    /// <summary>
    /// The <c>,</c> token between precision and scale, when parsed with a scale.
    /// </summary>
    public Token? CommaToken { get; init; }

    /// <summary>The scale (second) argument token, when parsed with a scale.</summary>
    public Token? ScaleToken { get; init; }

    /// <summary>
    /// The <c>)</c> token closing the arguments, when parsed with arguments.
    /// </summary>
    public Token? CloseParenToken { get; init; }

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            if (Schema != null)
            {
                yield return Schema;
                yield return SchemaDotToken ?? Token.Punctuation(TokenKind.Dot, NsqlSymbols.Dot);
            }
            yield return Name;
            if (Arguments is not null)
            {
                yield return OpenParenToken ?? Token.Punctuation(TokenKind.LeftParen, NsqlSymbols.LeftParen);
                if (PrecisionToken is { } precision)
                {
                    yield return precision;
                    if (CommaToken is { } comma)
                    {
                        yield return comma;
                    }
                    if (ScaleToken is { } scale)
                    {
                        yield return scale;
                    }
                }
                else
                {
                    // A synthetic type carries its arguments as one verbatim span; the parser re-splits them.
                    yield return Token.Span(Arguments);
                }
                yield return CloseParenToken ?? Token.Punctuation(TokenKind.RightParen, NsqlSymbols.RightParen);
            }
        }
    }
}
