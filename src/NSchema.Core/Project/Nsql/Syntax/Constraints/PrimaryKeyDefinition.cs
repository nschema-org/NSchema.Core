using NSchema.Project.Nsql.Syntax.Tables;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Constraints;

/// <summary>
/// <c>CONSTRAINT name PRIMARY KEY (columns)</c>.
/// </summary>
/// <param name="Name">The constraint name.</param>
/// <param name="Columns">The key columns.</param>
public sealed record PrimaryKeyDefinition(Identifier Name, ColumnList Columns) : TableMember
{
    /// <summary>
    /// The <c>CONSTRAINT</c> keyword token.
    /// </summary>
    public Token ConstraintKeyword { get; init; } = Token.Keyword(NsqlKeywords.Constraint);

    /// <summary>
    /// The <c>PRIMARY</c> keyword token.
    /// </summary>
    public Token PrimaryKeyword { get; init; } = Token.Keyword(NsqlKeywords.Primary);

    /// <summary>
    /// The <c>KEY</c> keyword token.
    /// </summary>
    public Token KeyKeyword { get; init; } = Token.Keyword(NsqlKeywords.Key);

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            if (DocComment is { } doc)
            {
                yield return doc;
            }
            yield return ConstraintKeyword;
            yield return Name;
            yield return PrimaryKeyword;
            yield return KeyKeyword;
            yield return Columns;
        }
    }
}
