using System.Diagnostics;

namespace NSchema.Schema;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public record TableIndex(string Name, IReadOnlyList<string> ColumnNames, bool IsUnique = false, string? Comment = null, string? Predicate = null)
{
    private string DebuggerDisplay =>
        $"{Name}: ({string.Join(", ", ColumnNames)})" +
        (IsUnique ? " UNIQUE" : "") +
        (Predicate is { } p ? $" WHERE {p}" : "");

    public virtual bool Equals(TableIndex? other) =>
        other != null
        && Name == other.Name
        && IsUnique == other.IsUnique
        && ColumnNames.SequenceEqual(other.ColumnNames)
        && Predicate == other.Predicate;

    public override int GetHashCode() => HashCode.Combine(Name, ColumnNames, IsUnique, Predicate);
}
