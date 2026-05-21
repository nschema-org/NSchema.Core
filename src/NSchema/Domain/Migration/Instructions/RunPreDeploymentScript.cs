using NSchema.Domain.Schema;

namespace NSchema.Domain.Migration.Instructions;

public sealed record RunPreDeploymentScript(Script Script) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
