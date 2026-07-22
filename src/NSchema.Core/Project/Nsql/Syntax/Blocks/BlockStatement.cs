using NSchema.Project.Nsql.Syntax;
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
    /// The <c>(</c> token opening the attributes, when parsed.
    /// </summary>
    public Token? OpenParenToken { get; init; }

    /// <summary>
    /// The <c>)</c> token closing the attributes, when parsed.
    /// </summary>
    public Token? CloseParenToken { get; init; }

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
            if (KeywordToken is { } keyword)
            {
                yield return keyword;
            }
            if (Label is { } label)
            {
                yield return label;
            }
            if (OpenParenToken is { } open)
            {
                yield return open;
            }
            foreach (var child in Attributes.Children)
            {
                yield return child;
            }
            if (CloseParenToken is { } close)
            {
                yield return close;
            }
            if (SemicolonToken is { } semicolon)
            {
                yield return semicolon;
            }
        }
    }
}
