using NSchema.Model;
using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Syntax.Tables;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Constraints;

/// <summary>
/// <c>CONSTRAINT name EXCLUDE [USING method] (element WITH operator, …) [WHERE (predicate)]</c>.
/// </summary>
/// <param name="Name">The constraint name.</param>
/// <param name="Elements">The exclusion elements.</param>
/// <param name="Method">The access method after <c>USING</c>, or <see langword="null"/>.</param>
/// <param name="Predicate">The partial-constraint predicate, or <see langword="null"/>.</param>
public sealed record ExclusionDefinition(
    Identifier Name,
    SeparatedSyntaxList<ExclusionElement> Elements,
    Identifier? Method = null,
    SqlText? Predicate = null
) : TableMember
{
    /// <summary>
    /// The <c>CONSTRAINT</c> keyword token, when parsed.
    /// </summary>
    public Token? ConstraintKeyword { get; init; }

    /// <summary>
    /// The <c>EXCLUDE</c> keyword token, when parsed.
    /// </summary>
    public Token? ExcludeKeyword { get; init; }

    /// <summary>
    /// The <c>USING</c> keyword token, when parsed with a method.
    /// </summary>
    public Token? UsingKeyword { get; init; }

    /// <summary>
    /// The <c>(</c> token opening the elements, when parsed.
    /// </summary>
    public Token? OpenParenToken { get; init; }

    /// <summary>
    /// The <c>)</c> token closing the elements, when parsed.
    /// </summary>
    public Token? CloseParenToken { get; init; }

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
            if (ExcludeKeyword is { } exclude)
            {
                yield return exclude;
            }
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
            foreach (var child in Elements.Children)
            {
                yield return child;
            }
            if (CloseParenToken is { } close)
            {
                yield return close;
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
        }
    }
}
