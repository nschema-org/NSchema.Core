using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Enums;

/// <summary>
/// <c>CREATE ENUM schema.name [RENAMED FROM old] ('value', …);</c>
/// </summary>
/// <param name="Name">The enum name as written.</param>
/// <param name="Values">The values in declaration order.</param>
public sealed record CreateEnumStatement(
    QualifiedName Name,
    SeparatedSyntaxList<EnumValue> Values
) : NsqlStatement
{
    /// <summary>
    /// The <c>CREATE</c> keyword token, when parsed.
    /// </summary>
    public Token? CreateKeyword { get; init; }

    /// <summary>
    /// The <c>ENUM</c> keyword token, when parsed.
    /// </summary>
    public Token? EnumKeyword { get; init; }

    /// <summary>
    /// The <c>(</c> token opening the value list, when parsed.
    /// </summary>
    public Token? OpenParenToken { get; init; }

    /// <summary>
    /// The <c>)</c> token closing the value list, when parsed.
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
            if (EnumKeyword is { } enumKeyword)
            {
                yield return enumKeyword;
            }
            yield return Name;
            if (OpenParenToken is { } open)
            {
                yield return open;
            }
            foreach (var child in Values.Children)
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
