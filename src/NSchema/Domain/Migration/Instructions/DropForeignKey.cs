namespace NSchema.Domain.Migration.Instructions;

public sealed record DropForeignKey(
    string SchemaName,
    string TableName,
    string ForeignKeyName
) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
