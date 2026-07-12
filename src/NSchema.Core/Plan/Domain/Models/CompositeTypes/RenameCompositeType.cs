namespace NSchema.Plan.Domain.Models.CompositeTypes;

/// <summary>
/// Represents renaming an existing composite type.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the composite type.</param>
/// <param name="OldName">The current name of the composite type.</param>
/// <param name="NewName">The new name of the composite type.</param>
public sealed record RenameCompositeType(string SchemaName, string OldName, string NewName) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
