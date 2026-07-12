using NSchema.Project.Domain.Models.CompositeTypes;

namespace NSchema.Plan.Domain.Models.CompositeTypes;

/// <summary>
/// Represents the creation of a composite type, with its fields inline.
/// </summary>
/// <param name="SchemaName">The name of the schema the composite type belongs to.</param>
/// <param name="CompositeType">The definition of the composite type to create.</param>
public sealed record CreateCompositeType(string SchemaName, CompositeType CompositeType) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
