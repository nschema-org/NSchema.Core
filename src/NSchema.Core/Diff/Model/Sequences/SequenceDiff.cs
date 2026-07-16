using NSchema.Model;
using NSchema.Model.Sequences;

namespace NSchema.Diff.Model.Sequences;

/// <summary>
/// Describes a change to a sequence.
/// </summary>
/// <param name="Schema">The name of the schema the sequence belongs to.</param>
/// <param name="Name">The sequence name.</param>
/// <param name="Kind">The change to the sequence.</param>
/// <param name="RenamedFrom">The previous sequence name when the sequence is being renamed; otherwise <see langword="null"/>.</param>
/// <param name="Definition">The sequence definition for an added sequence; otherwise <see langword="null"/>.</param>
/// <param name="Options">The change to the sequence's options, if any.</param>
/// <param name="Comment">The change to the sequence's comment, if any.</param>
public sealed record SequenceDiff(
    SqlIdentifier Schema,
    SqlIdentifier Name,
    ChangeKind Kind,
    SqlIdentifier? RenamedFrom = null,
    Sequence? Definition = null,
    ValueChange<SequenceOptions>? Options = null,
    ValueChange<string>? Comment = null
) : ISchemaObjectDiff;
