using NSchema.Domain.Schema;

namespace NSchema.Domain.Migration.Instructions;

public sealed record CreateTable(string SchemaName, Table Table) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
