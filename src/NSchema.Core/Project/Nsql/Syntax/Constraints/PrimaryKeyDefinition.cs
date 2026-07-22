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
    /// The <c>CONSTRAINT</c> keyword token, when parsed.
    /// </summary>
    public Token? ConstraintKeyword { get; init; }

    /// <summary>
    /// The <c>PRIMARY</c> keyword token, when parsed.
    /// </summary>
    public Token? PrimaryKeyword { get; init; }

    /// <summary>
    /// The <c>KEY</c> keyword token, when parsed.
    /// </summary>
    public Token? KeyKeyword { get; init; }

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
            if (PrimaryKeyword is { } primary)
            {
                yield return primary;
            }
            if (KeyKeyword is { } key)
            {
                yield return key;
            }
            yield return Columns;
        }
    }
}
