using NSchema.Diff.Model;
using NSchema.Schema.Model.Schemas;
using NSchema.Schema.Model.Sequences;

namespace NSchema.Diff;

internal sealed partial class SchemaComparer
{
    private static List<SequenceDiff> CompareSequences(string schemaName, IReadOnlyList<Sequence> current, SchemaDefinition desired) =>
        CompareObjects(schemaName, "sequence", current, desired.Sequences, desired.DroppedSequences, desired.IsPartial,
            sequence => new SequenceDiff(schemaName, sequence.Name, ChangeKind.Remove),
            sequence => BuildNewSequence(schemaName, sequence),
            (currentSequence, desiredSequence) => BuildModifiedSequence(schemaName, currentSequence, desiredSequence));

    private static SequenceDiff BuildNewSequence(string schema, Sequence sequence) =>
        new(schema, sequence.Name, ChangeKind.Add, Definition: sequence,
            Comment: ValueChanges.Changed(null, sequence.Comment));

    private static SequenceDiff? BuildModifiedSequence(string schema, Sequence current, Sequence desired)
    {
        var renamedFrom = current.Name == desired.Name ? null : current.Name;
        var comment = ValueChanges.Changed(current.Comment, desired.Comment);
        var options = ValueChanges.Changed(current.Options, desired.Options);

        if (renamedFrom is null && options is null && comment is null)
        {
            return null;
        }

        return new SequenceDiff(schema, desired.Name, ChangeKind.Modify, renamedFrom, null, options, comment);
    }
}
