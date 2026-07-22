using NSchema.Project.Nsql.Syntax.Tables;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Constraints;

/// <summary>
/// <c>CONSTRAINT name UNIQUE (columns)</c>.
/// </summary>
/// <param name="Name">The constraint name.</param>
/// <param name="Columns">The unique columns.</param>
public sealed record UniqueDefinition(Identifier Name, ColumnList Columns) : TableMember
{
    /// <summary>
    /// The <c>CONSTRAINT</c> keyword token, when parsed.
    /// </summary>
    public Token? ConstraintKeyword { get; init; }

    /// <summary>
    /// The <c>UNIQUE</c> keyword token, when parsed.
    /// </summary>
    public Token? UniqueKeyword { get; init; }

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
            if (UniqueKeyword is { } unique)
            {
                yield return unique;
            }
            yield return Columns;
        }
    }
}
