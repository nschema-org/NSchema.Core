using System.Diagnostics;

namespace NSchema.Schema;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public record PrimaryKey(string Name, IReadOnlyList<string> ColumnNames)
{
    private string DebuggerDisplay => $"{Name}: ({string.Join(", ", ColumnNames)})";

    public virtual bool Equals(PrimaryKey? other) =>
        other != null
         && Name == other.Name
         && ColumnNames.SequenceEqual(other.ColumnNames);

    public override int GetHashCode() => HashCode.Combine(Name, ColumnNames);
}
