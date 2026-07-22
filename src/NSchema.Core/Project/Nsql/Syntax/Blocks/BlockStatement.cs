using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Blocks;

/// <summary>
/// A block: <c>KEYWORD [label] ( key = value, … );</c>. One shape for every keyword; the
/// <paramref name="Keyword"/> says which. The configuration file and the lockfile are both sequences of these.
/// </summary>
/// <param name="Keyword">The keyword the block leads with.</param>
/// <param name="Label">The optional bare label (e.g. the <c>postgres</c> in <c>DATABASE postgres (…)</c>).</param>
/// <param name="Attributes">The attribute list.</param>
public sealed record BlockStatement(BlockKeyword Keyword, Identifier? Label, SeparatedSyntaxList<BlockAttribute> Attributes) : NsqlStatement
{
    /// <summary>
    /// The block's leading keyword token, when parsed.
    /// </summary>
    public Token? KeywordToken { get; init; }

    /// <summary>
    /// The <c>(</c> token opening the attributes.
    /// </summary>
    public Token OpenParenToken { get; init; } = Token.Punctuation(TokenKind.LeftParen, NsqlSymbols.LeftParen);

    /// <summary>
    /// The <c>)</c> token closing the attributes.
    /// </summary>
    public Token CloseParenToken { get; init; } = Token.Punctuation(TokenKind.RightParen, NsqlSymbols.RightParen);

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
            yield return KeywordToken ?? Token.Keyword(KeywordText());
            if (Label is { } label)
            {
                yield return label;
            }
            yield return OpenParenToken;
            foreach (var child in Attributes.Children)
            {
                yield return child;
            }
            yield return CloseParenToken;
            yield return SemicolonToken;
        }
    }

    private string KeywordText() => Keyword switch
    {
        BlockKeyword.Plugin => NsqlKeywords.Plugin,
        BlockKeyword.Engine => NsqlKeywords.Engine,
        BlockKeyword.Database => NsqlKeywords.Database,
        BlockKeyword.State => NsqlKeywords.State,
        _ => NsqlKeywords.Lock,
    };
}
