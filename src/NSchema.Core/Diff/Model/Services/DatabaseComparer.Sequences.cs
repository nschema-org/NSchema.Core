using NSchema.Diff.Model.Sequences;
using NSchema.Model;
using NSchema.Model.Schemas;
using NSchema.Model.Sequences;

namespace NSchema.Diff.Model.Services;

internal sealed partial class DatabaseComparer
{
    private static List<SequenceDiff> CompareSequences(SqlIdentifier schemaName, SqlIdentifier currentSchemaName, IReadOnlyList<Sequence> current, Schema desired, DirectiveLookup directives) =>
        CompareObjects(schemaName, "sequence", current, desired.Sequences,
            directives.Renames(ObjectKind.Sequence, currentSchemaName),
            sequence => new SequenceDiff(schemaName, sequence.Name, ChangeKind.Remove),
            sequence => BuildNewSequence(schemaName, sequence),
            (currentSequence, desiredSequence) => BuildModifiedSequence(schemaName, currentSequence, desiredSequence));

    private static SequenceDiff BuildNewSequence(SqlIdentifier schema, Sequence sequence) =>
        new(schema, sequence.Name, ChangeKind.Add, Definition: sequence,
            Comment: ValueChanges.Changed(null, sequence.Comment));

    private static SequenceDiff? BuildModifiedSequence(SqlIdentifier schema, Sequence current, Sequence desired)
    {
        var renamedFrom = current.Name == desired.Name ? (SqlIdentifier?)null : current.Name;
        var comment = ValueChanges.Changed(current.Comment, desired.Comment);
        var options = ValueChanges.Changed(current.Options, desired.Options);

        if (renamedFrom is null && options is null && comment is null)
        {
            return null;
        }

        return new SequenceDiff(schema, desired.Name, ChangeKind.Modify, renamedFrom, null, options, comment);
    }
}
