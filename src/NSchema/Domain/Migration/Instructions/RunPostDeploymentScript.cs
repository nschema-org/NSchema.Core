using NSchema.Domain.Schema;

namespace NSchema.Domain.Migration.Instructions;

public sealed record RunPostDeploymentScript(Script Script) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
