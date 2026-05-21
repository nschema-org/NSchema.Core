namespace NSchema.Domain.Migration.Instructions;

public sealed record SetColumnDefault(
    string SchemaName,
    string TableName,
    string ColumnName,
    string? OldDefault,
    string? NewDefault
) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
