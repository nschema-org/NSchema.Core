namespace NSchema.Plan.Model.Functions;

/// <summary>
/// Represents renaming an existing function.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the function.</param>
/// <param name="OldName">The current name of the function.</param>
/// <param name="NewName">The new name of the function.</param>
public sealed record RenameFunction(string SchemaName, string OldName, string NewName) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
