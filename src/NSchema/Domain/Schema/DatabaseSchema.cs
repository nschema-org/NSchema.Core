namespace NSchema.Domain.Schema;

public record DatabaseSchema(
    IReadOnlyList<Schema> Schemas,
    IReadOnlyList<Script>? PreDeploymentScripts = null,
    IReadOnlyList<Script>? PostDeploymentScripts = null
);
