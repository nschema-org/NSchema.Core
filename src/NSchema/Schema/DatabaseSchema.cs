using System.Diagnostics;

namespace NSchema.Schema;

[DebuggerDisplay("{Schemas.Count} schemas")]
public record DatabaseSchema(
    IReadOnlyList<SchemaDefinition> Schemas,
    IReadOnlyList<Script> PreDeploymentScripts,
    IReadOnlyList<Script> PostDeploymentScripts,
    IReadOnlyList<string>? DroppedSchemas = null
)
{
    public IReadOnlyList<string> DroppedSchemas { get; init; } = DroppedSchemas ?? [];

    public DatabaseSchema(IReadOnlyList<SchemaDefinition> Schemas)
        : this(Schemas, [], []) { }
}
