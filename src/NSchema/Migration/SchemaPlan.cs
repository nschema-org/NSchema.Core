using NSchema.Migration.Actions;

namespace NSchema.Migration;

public sealed record SchemaPlan(IReadOnlyList<SchemaAction> Actions)
{
    public bool IsEmpty => Actions.Count == 0;
}
