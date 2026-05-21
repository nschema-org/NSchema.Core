namespace NSchema.Domain.Migration.Instructions;

public sealed record RenameSchema(string OldName, string NewName) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
