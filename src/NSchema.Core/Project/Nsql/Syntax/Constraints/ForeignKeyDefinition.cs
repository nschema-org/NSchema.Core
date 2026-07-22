using NSchema.Project.Nsql.Syntax.Tables;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Constraints;

/// <summary>
/// <c>CONSTRAINT name FOREIGN KEY (columns) REFERENCES schema.table (columns) [ON DELETE action] [ON UPDATE action]</c>.
/// </summary>
/// <param name="Name">The constraint name.</param>
/// <param name="Columns">The referencing columns.</param>
/// <param name="References">The referenced table as written.</param>
/// <param name="ReferencedColumns">The referenced columns.</param>
/// <param name="OnDelete">The <c>ON DELETE</c> action (default <see cref="ReferentialAction.NoAction"/>).</param>
/// <param name="OnUpdate">The <c>ON UPDATE</c> action (default <see cref="ReferentialAction.NoAction"/>).</param>
public sealed record ForeignKeyDefinition(
    Identifier Name,
    ColumnList Columns,
    QualifiedName References,
    ColumnList ReferencedColumns,
    ReferentialAction OnDelete = ReferentialAction.NoAction,
    ReferentialAction OnUpdate = ReferentialAction.NoAction
) : TableMember
{
    /// <summary>
    /// The <c>CONSTRAINT</c> keyword token, when parsed.
    /// </summary>
    public Token? ConstraintKeyword { get; init; }

    /// <summary>
    /// The <c>FOREIGN</c> keyword token, when parsed.
    /// </summary>
    public Token? ForeignKeyword { get; init; }

    /// <summary>
    /// The <c>KEY</c> keyword token, when parsed.
    /// </summary>
    public Token? KeyKeyword { get; init; }

    /// <summary>
    /// The <c>REFERENCES</c> keyword token, when parsed.
    /// </summary>
    public Token? ReferencesKeyword { get; init; }

    /// <summary>
    /// The verbatim span of the <c>ON DELETE</c>/<c>ON UPDATE</c> actions, when parsed with any.
    /// </summary>
    public Token? ActionsToken { get; init; }

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
            if (ForeignKeyword is { } foreign)
            {
                yield return foreign;
            }
            if (KeyKeyword is { } key)
            {
                yield return key;
            }
            yield return Columns;
            if (ReferencesKeyword is { } references)
            {
                yield return references;
            }
            yield return References;
            yield return ReferencedColumns;
            if (ActionsToken is { } actions)
            {
                yield return actions;
            }
        }
    }
}
