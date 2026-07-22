using NSchema.Model;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Indexes;

/// <summary>
/// <c>CREATE [UNIQUE] INDEX name ON schema.relation [USING method] (keys) [INCLUDE (columns)] [WHERE (predicate)];</c>
/// </summary>
/// <param name="Name">The index name.</param>
/// <param name="IsUnique">Whether the index is declared <c>UNIQUE</c>.</param>
/// <param name="On">The table or materialized view the index attaches to.</param>
/// <param name="Columns">The index keys.</param>
/// <param name="Method">The access method after <c>USING</c>, or <see langword="null"/>.</param>
/// <param name="Include">The <c>INCLUDE</c> columns, or <see langword="null"/> when absent.</param>
/// <param name="Predicate">The partial-index predicate, or <see langword="null"/>.</param>
public sealed record CreateIndexStatement(
    Identifier Name,
    bool IsUnique,
    QualifiedName On,
    SeparatedSyntaxList<IndexElement> Columns,
    Identifier? Method = null,
    ColumnList? Include = null,
    SqlText? Predicate = null
) : NsqlStatement
{
    /// <summary>
    /// The <c>CREATE</c> keyword token.
    /// </summary>
    public Token CreateKeyword { get; init; } = Token.Keyword(NsqlKeywords.Create);

    /// <summary>
    /// The <c>UNIQUE</c> keyword token, when written unique.
    /// </summary>
    public Token? UniqueKeyword { get; init; }

    /// <summary>
    /// The <c>INDEX</c> keyword token.
    /// </summary>
    public Token IndexKeyword { get; init; } = Token.Keyword(NsqlKeywords.Index);

    /// <summary>
    /// The <c>ON</c> keyword token.
    /// </summary>
    public Token OnKeyword { get; init; } = Token.Keyword(NsqlKeywords.On);

    /// <summary>
    /// The <c>USING</c> keyword token, when written with a method.
    /// </summary>
    public Token? UsingKeyword { get; init; }

    /// <summary>
    /// The <c>(</c> token opening the keys.
    /// </summary>
    public Token OpenParenToken { get; init; } = Token.Punctuation(TokenKind.LeftParen, NsqlSymbols.LeftParen);

    /// <summary>
    /// The <c>)</c> token closing the keys.
    /// </summary>
    public Token CloseParenToken { get; init; } = Token.Punctuation(TokenKind.RightParen, NsqlSymbols.RightParen);

    /// <summary>
    /// The <c>INCLUDE</c> keyword token, when written with included columns.
    /// </summary>
    public Token? IncludeKeyword { get; init; }

    /// <summary>
    /// The <c>WHERE</c> keyword token, when written with a predicate.
    /// </summary>
    public Token? WhereKeyword { get; init; }

    /// <summary>
    /// The <c>(</c> token opening the predicate, when written with a predicate.
    /// </summary>
    public Token? WhereOpenParenToken { get; init; }

    /// <summary>
    /// The verbatim predicate span token, when parsed with a predicate.
    /// </summary>
    public Token? PredicateToken { get; init; }

    /// <summary>
    /// The <c>)</c> token closing the predicate, when written with a predicate.
    /// </summary>
    public Token? WhereCloseParenToken { get; init; }

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
            if (IsUnique)
            {
                yield return UniqueKeyword ?? Token.Keyword(NsqlKeywords.Unique);
            }
            yield return IndexKeyword;
            yield return Name;
            yield return OnKeyword;
            yield return On;
            if (Method is { } method)
            {
                yield return UsingKeyword ?? Token.Keyword(NsqlKeywords.Using);
                yield return method;
            }
            yield return OpenParenToken;
            foreach (var child in Columns.Children)
            {
                yield return child;
            }
            yield return CloseParenToken;
            if (Include is { } include)
            {
                yield return IncludeKeyword ?? Token.Keyword(NsqlKeywords.Include);
                yield return include;
            }
            if (Predicate is { } predicate)
            {
                yield return WhereKeyword ?? Token.Keyword(NsqlKeywords.Where);
                yield return WhereOpenParenToken ?? Token.Punctuation(TokenKind.LeftParen, NsqlSymbols.LeftParen);
                yield return PredicateToken ?? Token.Span(predicate.Value);
                yield return WhereCloseParenToken ?? Token.Punctuation(TokenKind.RightParen, NsqlSymbols.RightParen);
            }
            yield return SemicolonToken;
        }
    }
}
