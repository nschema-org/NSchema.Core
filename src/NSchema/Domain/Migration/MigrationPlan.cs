using NSchema.Domain.Migration.Instructions;

namespace NSchema.Domain.Migration;

public sealed record MigrationPlan(IReadOnlyList<SchemaInstruction> Instructions)
{
    public bool IsEmpty => Instructions.Count == 0;
}
