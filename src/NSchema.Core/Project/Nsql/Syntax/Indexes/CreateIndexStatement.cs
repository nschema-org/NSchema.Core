using NSchema.Model;
using NSchema.Project.Nsql.Syntax;
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
    /// The <c>CREATE</c> keyword token, when parsed.
    /// </summary>
    public Token? CreateKeyword { get; init; }

    /// <summary>
    /// The <c>UNIQUE</c> keyword token, when parsed unique.
    /// </summary>
    public Token? UniqueKeyword { get; init; }

    /// <summary>
    /// The <c>INDEX</c> keyword token, when parsed.
    /// </summary>
    public Token? IndexKeyword { get; init; }

    /// <summary>
    /// The <c>ON</c> keyword token, when parsed.
    /// </summary>
    public Token? OnKeyword { get; init; }

    /// <summary>
    /// The <c>USING</c> keyword token, when parsed with a method.
    /// </summary>
    public Token? UsingKeyword { get; init; }

    /// <summary>
    /// The <c>(</c> token opening the keys, when parsed.
    /// </summary>
    public Token? OpenParenToken { get; init; }

    /// <summary>
    /// The <c>)</c> token closing the keys, when parsed.
    /// </summary>
    public Token? CloseParenToken { get; init; }

    /// <summary>
    /// The <c>INCLUDE</c> keyword token, when parsed with included columns.
    /// </summary>
    public Token? IncludeKeyword { get; init; }

    /// <summary>
    /// The <c>WHERE</c> keyword token, when parsed with a predicate.
    /// </summary>
    public Token? WhereKeyword { get; init; }

    /// <summary>
    /// The <c>(</c> token opening the predicate, when parsed with a predicate.
    /// </summary>
    public Token? WhereOpenParenToken { get; init; }

    /// <summary>
    /// The verbatim predicate span token, when parsed with a predicate.
    /// </summary>
    public Token? PredicateToken { get; init; }

    /// <summary>
    /// The <c>)</c> token closing the predicate, when parsed with a predicate.
    /// </summary>
    public Token? WhereCloseParenToken { get; init; }

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
            if (UniqueKeyword is { } unique)
            {
                yield return unique;
            }
            if (IndexKeyword is { } index)
            {
                yield return index;
            }
            yield return Name;
            if (OnKeyword is { } on)
            {
                yield return on;
            }
            yield return On;
            if (UsingKeyword is { } usingKeyword)
            {
                yield return usingKeyword;
            }
            if (Method is { } method)
            {
                yield return method;
            }
            if (OpenParenToken is { } open)
            {
                yield return open;
            }
            foreach (var child in Columns.Children)
            {
                yield return child;
            }
            if (CloseParenToken is { } close)
            {
                yield return close;
            }
            if (IncludeKeyword is { } includeKeyword)
            {
                yield return includeKeyword;
            }
            if (Include is { } include)
            {
                yield return include;
            }
            if (WhereKeyword is { } where)
            {
                yield return where;
            }
            if (WhereOpenParenToken is { } whereOpen)
            {
                yield return whereOpen;
            }
            if (PredicateToken is { } predicate)
            {
                yield return predicate;
            }
            if (WhereCloseParenToken is { } whereClose)
            {
                yield return whereClose;
            }
            if (SemicolonToken is { } semicolon)
            {
                yield return semicolon;
            }
        }
    }
}
