namespace NSchema.Domain.Migration.Instructions;

public sealed record DropSchema(string SchemaName) : SchemaInstruction
{
    public override bool IsDestructive => true;
}
