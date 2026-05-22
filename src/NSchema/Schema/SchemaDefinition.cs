using System.Diagnostics;

namespace NSchema.Schema;

[DebuggerDisplay("{Name,nq} ({Tables.Count} tables)")]
public record SchemaDefinition(
    string Name,
    IReadOnlyList<Table> Tables,
    string? PreviousName = null,
    bool IsPartial = false,
    IReadOnlyList<string>? DroppedTables = null,
    string? Comment = null,
    IReadOnlyList<SchemaGrant>? Grants = null
);
