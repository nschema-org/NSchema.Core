namespace NSchema.Domain.Schema;

public record DatabaseSchema(
    IReadOnlyList<Schema> Schemas,
    IReadOnlyList<Script> PreDeploymentScripts,
    IReadOnlyList<Script> PostDeploymentScripts
)
{
    public DatabaseSchema(IReadOnlyList<Schema> Schemas)
        : this(Schemas, [], []) { }
}
