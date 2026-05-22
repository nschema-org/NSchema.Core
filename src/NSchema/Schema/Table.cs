using System.Diagnostics;

namespace NSchema.Schema;

[DebuggerDisplay("{Name,nq} ({Columns.Count} columns)")]
public record Table(
    string Name,
    IReadOnlyList<Column> Columns,
    PrimaryKey? PrimaryKey = null,
    IReadOnlyList<ForeignKey>? ForeignKeys = null,
    IReadOnlyList<TableIndex>? Indexes = null,
    string? PreviousName = null,
    string? Comment = null,
    IReadOnlyList<TableGrant>? Grants = null
);
