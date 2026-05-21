namespace NSchema.Domain.Migration.Instructions;

public sealed record DropTable(string SchemaName, string TableName) : SchemaInstruction
{
    public override bool IsDestructive => true;
}
