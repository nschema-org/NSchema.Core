using System.Diagnostics;

namespace NSchema.Schema;

[DebuggerDisplay("{Name,nq} ({Tables.Count} tables)")]
public record SchemaDefinition(
    string Name,
    string? PreviousName = null,
    bool IsPartial = false,
    string? Comment = null,
    IReadOnlyList<Table>? Tables = null,
    IReadOnlyList<string>? DroppedTables = null,
    IReadOnlyList<SchemaGrant>? Grants = null
)
{
    public IReadOnlyList<Table> Tables { get; init; } = Tables ?? [];
    public IReadOnlyList<string> DroppedTables { get; init; } = DroppedTables ?? [];
    public IReadOnlyList<SchemaGrant> Grants { get; init; } = Grants ?? [];
}
