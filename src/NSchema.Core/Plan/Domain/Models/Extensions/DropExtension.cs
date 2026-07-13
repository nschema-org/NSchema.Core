using NSchema.Project.Domain.Models;
namespace NSchema.Plan.Domain.Models.Extensions;

/// <summary>
/// Represents the removal of an existing database extension.
/// </summary>
/// <param name="ExtensionName">The name of the extension to remove.</param>
public sealed record DropExtension(SqlIdentifier ExtensionName) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => true;
}
