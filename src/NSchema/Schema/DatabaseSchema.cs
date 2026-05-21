namespace NSchema.Schema;

public record DatabaseSchema(
    IReadOnlyList<SchemaDefinition> Schemas,
    IReadOnlyList<Script> PreDeploymentScripts,
    IReadOnlyList<Script> PostDeploymentScripts,
    IReadOnlyList<string>? DroppedSchemas = null
)
{
    public DatabaseSchema(IReadOnlyList<SchemaDefinition> Schemas)
        : this(Schemas, [], []) { }
}
