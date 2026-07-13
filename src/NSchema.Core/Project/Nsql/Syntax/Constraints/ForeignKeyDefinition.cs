using NSchema.Project.Nsql.Syntax.Tables;

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
    IReadOnlyList<Identifier> Columns,
    QualifiedName References,
    IReadOnlyList<Identifier> ReferencedColumns,
    ReferentialAction OnDelete = ReferentialAction.NoAction,
    ReferentialAction OnUpdate = ReferentialAction.NoAction
) : TableMember;