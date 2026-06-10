using NSchema.Schema.Model;

namespace NSchema.Plan.Model;

/// <summary>
/// Represents the creation of a sequence.
/// </summary>
/// <param name="SchemaName">The name of the schema the sequence belongs to.</param>
/// <param name="Sequence">The definition of the sequence to create.</param>
public sealed record CreateSequence(string SchemaName, Sequence Sequence) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
