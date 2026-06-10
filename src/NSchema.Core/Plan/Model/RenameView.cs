namespace NSchema.Plan.Model;

/// <summary>
/// Represents renaming an existing view.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the view.</param>
/// <param name="OldName">The current name of the view.</param>
/// <param name="NewName">The new name of the view.</param>
public sealed record RenameView(string SchemaName, string OldName, string NewName) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
