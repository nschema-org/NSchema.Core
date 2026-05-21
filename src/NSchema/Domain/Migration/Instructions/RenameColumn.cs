namespace NSchema.Domain.Migration.Instructions;

public sealed record RenameColumn(
    string SchemaName,
    string TableName,
    string OldName,
    string NewName
) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
