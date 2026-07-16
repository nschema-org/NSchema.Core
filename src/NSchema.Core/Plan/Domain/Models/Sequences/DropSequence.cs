using NSchema.Model;
namespace NSchema.Plan.Domain.Models.Sequences;

/// <summary>
/// Represents the removal of an existing sequence from the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the sequence to be removed.</param>
/// <param name="SequenceName">The name of the sequence to be removed.</param>
public sealed record DropSequence(SqlIdentifier SchemaName, SqlIdentifier SequenceName) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => true;
}
