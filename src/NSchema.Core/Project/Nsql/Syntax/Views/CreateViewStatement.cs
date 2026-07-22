using NSchema.Model;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Views;

/// <summary>
/// <c>CREATE [MATERIALIZED] VIEW schema.name [RENAMED FROM old] AS body;</c>
/// </summary>
/// <param name="Name">The view name as written.</param>
/// <param name="Body">The defining query, verbatim (the text after <c>AS</c>).</param>
/// <param name="IsMaterialized">Whether the view is materialized.</param>
public sealed record CreateViewStatement(
    QualifiedName Name,
    SqlText Body,
    bool IsMaterialized = false
) : NsqlStatement
{
    /// <summary>
    /// The <c>CREATE</c> keyword token.
    /// </summary>
    public Token CreateKeyword { get; init; } = Token.Keyword(NsqlKeywords.Create);

    /// <summary>
    /// The <c>MATERIALIZED</c> keyword token, when written materialized.
    /// </summary>
    public Token? MaterializedKeyword { get; init; }

    /// <summary>
    /// The <c>VIEW</c> keyword token.
    /// </summary>
    public Token ViewKeyword { get; init; } = Token.Keyword(NsqlKeywords.View);

    /// <summary>
    /// The <c>AS</c> keyword token.
    /// </summary>
    public Token AsKeyword { get; init; } = Token.Keyword(NsqlKeywords.As);

    /// <summary>
    /// The verbatim view-body span token, when parsed.
    /// </summary>
    public Token? BodyToken { get; init; }

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
            if (IsMaterialized)
            {
                yield return MaterializedKeyword ?? Token.Keyword(NsqlKeywords.Materialized);
            }
            yield return ViewKeyword;
            yield return Name;
            yield return AsKeyword;
            yield return BodyToken ?? Token.Span(Body.Value);
            yield return SemicolonToken;
        }
    }
}
