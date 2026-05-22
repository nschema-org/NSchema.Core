using System.Diagnostics;

namespace NSchema.Schema;

[DebuggerDisplay("{Name,nq} ({Columns.Count} columns)")]
public record Table(
    string Name,
    string? PreviousName = null,
    PrimaryKey? PrimaryKey = null,
    string? Comment = null,
    IReadOnlyList<Column>? Columns = null,
    IReadOnlyList<ForeignKey>? ForeignKeys = null,
    IReadOnlyList<TableIndex>? Indexes = null,
    IReadOnlyList<TableGrant>? Grants = null
)
{
    public IReadOnlyList<Column> Columns { get; init; } = Columns ?? [];
    public IReadOnlyList<ForeignKey> ForeignKeys { get; init; } = ForeignKeys ?? [];
    public IReadOnlyList<TableIndex> Indexes { get; init; } = Indexes ?? [];
    public IReadOnlyList<TableGrant> Grants { get; init; } = Grants ?? [];
}
