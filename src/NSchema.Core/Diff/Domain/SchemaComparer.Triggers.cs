using NSchema.Diff.Domain.Models.Triggers;
using NSchema.Project.Domain.Models.Triggers;

namespace NSchema.Diff.Domain;

internal sealed partial class SchemaComparer
{
    // Triggers are table members like indexes: matched by name, a structural change is a remove + add (Trigger's
    // Equals excludes the comment), and a comment-only change is an in-place modify.
    private List<TriggerDiff> CompareTriggers(string schemaName, string tableName, IReadOnlyList<Trigger> current, IReadOnlyList<Trigger> desired) =>
        CompareTableMembers(schemaName, tableName, "Trigger", current, desired,
            (kind, name, definition, comment) => new TriggerDiff(kind, name, definition, comment));
}
