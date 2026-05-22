using System.Diagnostics;

namespace NSchema.Schema;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public record ForeignKey(
    string Name,
    IReadOnlyList<string> ColumnNames,
    string ReferencedSchema,
    string ReferencedTable,
    IReadOnlyList<string> ReferencedColumnNames,
    ReferentialAction OnDelete = ReferentialAction.NoAction,
    ReferentialAction OnUpdate = ReferentialAction.NoAction
)
{
    private string DebuggerDisplay =>
        $"{Name}: ({string.Join(", ", ColumnNames)}) -> {ReferencedSchema}.{ReferencedTable} ({string.Join(", ", ReferencedColumnNames)})";

    public virtual bool Equals(ForeignKey? other) =>
        other != null
        && Name == other.Name
        && ColumnNames.SequenceEqual(other.ColumnNames)
        && ReferencedSchema == other.ReferencedSchema
        && ReferencedTable == other.ReferencedTable
        && ReferencedColumnNames.SequenceEqual(other.ReferencedColumnNames)
        && OnDelete == other.OnDelete
        && OnUpdate == other.OnUpdate;

    public override int GetHashCode() =>
        HashCode.Combine(Name, ColumnNames, ReferencedSchema, ReferencedTable, ReferencedColumnNames, OnDelete, OnUpdate);
}
