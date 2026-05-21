namespace NSchema.Domain.Migration.Instructions;

public sealed record CreateSchema(string SchemaName) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
