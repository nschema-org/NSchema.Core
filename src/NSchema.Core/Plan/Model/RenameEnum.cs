namespace NSchema.Plan.Model;

/// <summary>
/// Represents renaming an existing enum type.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the enum.</param>
/// <param name="OldName">The current name of the enum.</param>
/// <param name="NewName">The new name of the enum.</param>
public sealed record RenameEnum(string SchemaName, string OldName, string NewName) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
