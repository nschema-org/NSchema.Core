using NSchema.Diff.Model;
using NSchema.Schema.Model;

namespace NSchema.Diff;

internal sealed partial class SchemaComparer
{
    private List<SequenceDiff> CompareSequences(string schemaName, IReadOnlyList<Sequence> current, SchemaDefinition desired)
    {
        var result = new List<SequenceDiff>();
        var droppedSequences = desired.DroppedSequences;
        var (forDesired, currentMatched) = MatchEntities(current, desired.Sequences, s => s.Name, s => s.OldName, "sequence", schemaName);

        for (var j = 0; j < current.Count; j++)
        {
            var currentSequence = current[j];
            if (currentMatched[j])
            {
                continue;
            }

            // A sequence absent from the desired set is dropped — unless the schema is partial and it was not
            // named in an explicit DROP SEQUENCE, mirroring how unmanaged tables are left alone.
            if (droppedSequences.Contains(currentSequence.Name, StringComparer.OrdinalIgnoreCase) || !desired.IsPartial)
            {
                result.Add(new SequenceDiff(schemaName, currentSequence.Name, ChangeKind.Remove));
            }
        }

        for (var i = 0; i < desired.Sequences.Count; i++)
        {
            var desiredSequence = desired.Sequences[i];
            if (forDesired[i] is not { } matchingCurrent)
            {
                result.Add(BuildNewSequence(schemaName, desiredSequence));
            }
            else if (BuildModifiedSequence(schemaName, matchingCurrent, desiredSequence) is { } diff)
            {
                result.Add(diff);
            }
        }

        return result;
    }

    private static SequenceDiff BuildNewSequence(string schema, Sequence sequence) =>
        new(schema, sequence.Name, ChangeKind.Add, Definition: sequence,
            Comment: sequence.Comment is not null ? new ValueChange<string>(null, sequence.Comment) : null);

    private static SequenceDiff? BuildModifiedSequence(string schema, Sequence current, Sequence desired)
    {
        var renamedFrom = current.Name == desired.Name ? null : current.Name;
        var comment = current.Comment != desired.Comment ? new ValueChange<string>(current.Comment, desired.Comment) : null;
        var options = current.Options != desired.Options ? new ValueChange<SequenceOptions>(current.Options, desired.Options) : null;

        if (renamedFrom is null && options is null && comment is null)
        {
            return null;
        }

        return new SequenceDiff(schema, desired.Name, ChangeKind.Modify, renamedFrom, null, options, comment);
    }
}
