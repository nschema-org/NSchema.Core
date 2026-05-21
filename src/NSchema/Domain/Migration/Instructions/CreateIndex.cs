using NSchema.Domain.Schema;

namespace NSchema.Domain.Migration.Instructions;

public sealed record CreateIndex(
    string SchemaName,
    string TableName,
    TableIndex Index
) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
