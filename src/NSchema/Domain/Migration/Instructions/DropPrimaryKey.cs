namespace NSchema.Domain.Migration.Instructions;

public sealed record DropPrimaryKey(
    string SchemaName,
    string TableName,
    string PrimaryKeyName
) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
