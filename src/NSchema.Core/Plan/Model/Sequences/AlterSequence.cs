using NSchema.Model;
using NSchema.Model.Sequences;

namespace NSchema.Plan.Model.Sequences;

/// <summary>
/// Represents altering the options of an existing sequence.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the sequence.</param>
/// <param name="SequenceName">The name of the sequence whose options are to be altered.</param>
/// <param name="OldOptions">The sequence's options before the alteration.</param>
/// <param name="NewOptions">The sequence's options after the alteration.</param>
public sealed record AlterSequence(
    SqlIdentifier SchemaName,
    SqlIdentifier SequenceName,
    SequenceOptions OldOptions,
    SequenceOptions NewOptions
) : MigrationAction;
