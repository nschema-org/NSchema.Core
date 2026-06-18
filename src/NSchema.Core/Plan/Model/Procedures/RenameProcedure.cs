namespace NSchema.Plan.Model.Procedures;

/// <summary>
/// Represents renaming an existing procedure.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the procedure.</param>
/// <param name="OldName">The current name of the procedure.</param>
/// <param name="NewName">The new name of the procedure.</param>
public sealed record RenameProcedure(string SchemaName, string OldName, string NewName) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
