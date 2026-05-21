using NSchema.Domain.Schema;

namespace NSchema.Domain.Migration.Instructions;

public sealed record AddPrimaryKey(
    string SchemaName,
    string TableName,
    PrimaryKey PrimaryKey
) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
