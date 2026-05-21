using NSchema.Domain.Schema;

namespace NSchema.Domain.Migration.Instructions;

public sealed record AddForeignKey(
    string SchemaName,
    string TableName,
    ForeignKey ForeignKey
) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
