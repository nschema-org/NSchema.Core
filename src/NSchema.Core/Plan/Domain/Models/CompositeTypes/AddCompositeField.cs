using NSchema.Project.Domain.Models.CompositeTypes;

namespace NSchema.Plan.Domain.Models.CompositeTypes;

/// <summary>
/// Represents adding a field to a composite type (<c>ALTER TYPE … ADD ATTRIBUTE …</c>).
/// </summary>
/// <param name="SchemaName">The name of the schema containing the composite type.</param>
/// <param name="TypeName">The name of the composite type.</param>
/// <param name="Field">The field to add.</param>
public sealed record AddCompositeField(string SchemaName, string TypeName, CompositeField Field) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
