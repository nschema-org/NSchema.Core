namespace NSchema.Plan.Model;

/// <summary>
/// Represents renaming an existing sequence.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the sequence.</param>
/// <param name="OldName">The current name of the sequence.</param>
/// <param name="NewName">The new name of the sequence.</param>
public sealed record RenameSequence(string SchemaName, string OldName, string NewName) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
