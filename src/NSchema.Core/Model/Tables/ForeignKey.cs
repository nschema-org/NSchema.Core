using System.Diagnostics;

namespace NSchema.Model.Tables;

/// <summary>
/// Represents a foreign key constraint in a database schema.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class ForeignKey : DatabaseMember, IEquatable<ForeignKey>
{
    /// <summary>
    /// A list of column names in the current table that are part of the foreign key constraint.
    /// </summary>
    public required List<SqlIdentifier> ColumnNames { get; init; }

    /// <summary>
    /// The name of the schema that contains the referenced table.
    /// </summary>
    public required SqlIdentifier ReferencedSchema { get; set; }

    /// <summary>
    /// The name of the table that is referenced by the foreign key constraint.
    /// </summary>
    public required SqlIdentifier ReferencedTable { get; set; }

    /// <summary>
    /// A list of column names in the referenced table that are part of the foreign key constraint.
    /// </summary>
    public required List<SqlIdentifier> ReferencedColumnNames { get; init; }

    /// <summary>
    /// The referential action to be taken when a referenced row is deleted.
    /// </summary>
    public ReferentialAction OnDelete { get; set; }

    /// <summary>
    /// The referential action to be taken when a referenced row is updated.
    /// </summary>
    public ReferentialAction OnUpdate { get; set; }

    /// <inheritdoc/>
    public override ForeignKey Clone() => new()
    {
        Name = Name,
        ColumnNames = [.. ColumnNames],
        ReferencedSchema = ReferencedSchema,
        ReferencedTable = ReferencedTable,
        ReferencedColumnNames = [.. ReferencedColumnNames],
        OnDelete = OnDelete,
        OnUpdate = OnUpdate,
        Comment = Comment,
    };

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
