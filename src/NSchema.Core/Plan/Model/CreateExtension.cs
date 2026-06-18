using NSchema.Schema.Model;

namespace NSchema.Plan.Model;

/// <summary>
/// Represents the creation of a database extension.
/// </summary>
/// <param name="Extension">The definition of the extension to create.</param>
public sealed record CreateExtension(Extension Extension) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
