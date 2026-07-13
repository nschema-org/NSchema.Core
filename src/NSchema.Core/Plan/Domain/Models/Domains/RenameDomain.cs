using NSchema.Project.Domain.Models;
namespace NSchema.Plan.Domain.Models.Domains;

/// <summary>
/// Represents renaming an existing domain.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the domain.</param>
/// <param name="OldName">The current name of the domain.</param>
/// <param name="NewName">The new name of the domain.</param>
public sealed record RenameDomain(SqlIdentifier SchemaName, SqlIdentifier OldName, SqlIdentifier NewName) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
