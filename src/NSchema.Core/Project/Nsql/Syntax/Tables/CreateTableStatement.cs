using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Tables;

/// <summary>
/// <c>CREATE TABLE schema.name [RENAMED FROM old] ( members… );</c>
/// </summary>
/// <param name="Name">The table name as written.</param>
/// <param name="Members">The body members in declaration order (columns, constraints, indexes, includes).</param>
public sealed record CreateTableStatement(
    QualifiedName Name,
    SeparatedSyntaxList<TableMember> Members
) : NsqlStatement
{
    /// <summary>
    /// The <c>CREATE</c> keyword token, when parsed.
    /// </summary>
    public Token? CreateKeyword { get; init; }

    /// <summary>
    /// The <c>TABLE</c> keyword token, when parsed.
    /// </summary>
    public Token? TableKeyword { get; init; }

    /// <summary>
    /// The <c>(</c> token opening the body, when parsed.
    /// </summary>
    public Token? OpenParenToken { get; init; }

    /// <summary>
    /// The <c>)</c> token closing the body, when parsed.
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
            if (TableKeyword is { } table)
            {
                yield return table;
            }
            yield return Name;
            if (OpenParenToken is { } open)
            {
                yield return open;
            }
            foreach (var child in Members.Children)
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
