using System.Diagnostics;

namespace NSchema.Model.Tables;

/// <summary>
/// Represents a foreign key constraint in a database schema.
/// </summary>
/// <param name="name">The name of the foreign key constraint.</param>
/// <param name="columnNames">A list of column names in the current table that are part of the foreign key constraint.</param>
/// <param name="referencedSchema">The name of the schema that contains the referenced table.</param>
/// <param name="referencedTable">The name of the table that is referenced by the foreign key constraint.</param>
/// <param name="referencedColumnNames">A list of column names in the referenced table that are part of the foreign key constraint.</param>
/// <param name="onDelete">The referential action to be taken when a referenced row is deleted (e.g., CASCADE, SET NULL, NO ACTION).</param>
/// <param name="onUpdate">The referential action to be taken when a referenced row is updated (e.g., CASCADE, SET NULL, NO ACTION).</param>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class ForeignKey(
    SqlIdentifier name,
    List<SqlIdentifier> columnNames,
    SqlIdentifier referencedSchema,
    SqlIdentifier referencedTable,
    List<SqlIdentifier> referencedColumnNames,
    ReferentialAction onDelete = ReferentialAction.NoAction,
    ReferentialAction onUpdate = ReferentialAction.NoAction
) : DatabaseMember(name), IEquatable<ForeignKey>
{

    /// <summary>
    /// A list of column names in the current table that are part of the foreign key constraint.
    /// </summary>
    public List<SqlIdentifier> ColumnNames { get; } = columnNames ?? [];

    /// <summary>
    /// The name of the schema that contains the referenced table.
    /// </summary>
    public SqlIdentifier ReferencedSchema { get; set; } = referencedSchema;

    /// <summary>
    /// The name of the table that is referenced by the foreign key constraint.
    /// </summary>
    public SqlIdentifier ReferencedTable { get; set; } = referencedTable;

    /// <summary>
    /// A list of column names in the referenced table that are part of the foreign key constraint.
    /// </summary>
    public List<SqlIdentifier> ReferencedColumnNames { get; } = referencedColumnNames ?? [];

    /// <summary>
    /// The referential action to be taken when a referenced row is deleted.
    /// </summary>
    public ReferentialAction OnDelete { get; set; } = onDelete;

    /// <summary>
    /// The referential action to be taken when a referenced row is updated.
    /// </summary>
    public ReferentialAction OnUpdate { get; set; } = onUpdate;

    /// <inheritdoc/>
    public override ForeignKey Clone() =>
        new(Name, [.. ColumnNames], ReferencedSchema, ReferencedTable, [.. ReferencedColumnNames], OnDelete, OnUpdate) { Comment = Comment };

    /// <summary>
    /// Structural equality over the declared definition; the parent and the comment are excluded.
    /// </summary>
    public bool Equals(ForeignKey? other) =>
        other is not null
        && Name == other.Name
        && ColumnNames.SequenceEqual(other.ColumnNames)
        && ReferencedSchema == other.ReferencedSchema
        && ReferencedTable == other.ReferencedTable
        && ReferencedColumnNames.SequenceEqual(other.ReferencedColumnNames)
        && OnDelete == other.OnDelete
        && OnUpdate == other.OnUpdate;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ForeignKey other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(Name, ColumnNames.Count, ReferencedSchema, ReferencedTable, ReferencedColumnNames.Count, OnDelete, OnUpdate);

    private string DebuggerDisplay =>
        $"{Name}: ({string.Join(", ", ColumnNames)}) -> {ReferencedSchema}.{ReferencedTable} ({string.Join(", ", ReferencedColumnNames)})";
}
