using System.Diagnostics;

namespace NSchema.Schema;

[DebuggerDisplay("{Schemas.Count} schemas")]
public record DatabaseSchema(
    IReadOnlyList<SchemaDefinition> Schemas,
    IReadOnlyList<string>? DroppedSchemas = null
)
{
    public IReadOnlyList<string> DroppedSchemas { get; init; } = DroppedSchemas ?? [];
}
