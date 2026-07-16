using NSchema.Model;

namespace NSchema.Plan.Model.Sequences;

/// <summary>
/// Represents renaming an existing sequence.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the sequence.</param>
/// <param name="OldName">The current name of the sequence.</param>
/// <param name="NewName">The new name of the sequence.</param>
public sealed record RenameSequence(SqlIdentifier SchemaName, SqlIdentifier OldName, SqlIdentifier NewName) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
