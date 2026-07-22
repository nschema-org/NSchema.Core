using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.CompositeTypes;

/// <summary>
/// <c>CREATE TYPE schema.name [RENAMED FROM old] AS (field type, …);</c>
/// </summary>
/// <param name="Name">The composite type name as written.</param>
/// <param name="Fields">The fields in declaration order.</param>
public sealed record CreateCompositeTypeStatement(
    QualifiedName Name,
    SeparatedSyntaxList<CompositeFieldDefinition> Fields
) : NsqlStatement
{
    /// <summary>
    /// The <c>CREATE</c> keyword token, when parsed.
    /// </summary>
    public Token? CreateKeyword { get; init; }

    /// <summary>
    /// The <c>TYPE</c> keyword token, when parsed.
    /// </summary>
    public Token? TypeKeyword { get; init; }

    /// <summary>
    /// The <c>AS</c> keyword token, when parsed.
    /// </summary>
    public Token? AsKeyword { get; init; }

    /// <summary>
    /// The <c>(</c> token opening the field list, when parsed.
    /// </summary>
    public Token? OpenParenToken { get; init; }

    /// <summary>
    /// The <c>)</c> token closing the field list, when parsed.
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
            if (CreateKeyword is { } create)
            {
                yield return create;
            }
            if (TypeKeyword is { } typeKeyword)
            {
                yield return typeKeyword;
            }
            yield return Name;
            if (AsKeyword is { } asKeyword)
            {
                yield return asKeyword;
            }
            if (OpenParenToken is { } open)
            {
                yield return open;
            }
            foreach (var child in Fields.Children)
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
