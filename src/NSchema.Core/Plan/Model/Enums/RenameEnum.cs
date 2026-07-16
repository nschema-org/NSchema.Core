using NSchema.Model;

namespace NSchema.Plan.Model.Enums;

/// <summary>
/// Represents renaming an existing enum type.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the enum.</param>
/// <param name="OldName">The current name of the enum.</param>
/// <param name="NewName">The new name of the enum.</param>
public sealed record RenameEnum(SqlIdentifier SchemaName, SqlIdentifier OldName, SqlIdentifier NewName) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
