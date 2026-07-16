using NSchema.Model;
using NSchema.Model.CompositeTypes;

namespace NSchema.Plan.Model.CompositeTypes;

/// <summary>
/// Represents adding a field to a composite type (<c>ALTER TYPE … ADD ATTRIBUTE …</c>).
/// </summary>
/// <param name="SchemaName">The name of the schema containing the composite type.</param>
/// <param name="TypeName">The name of the composite type.</param>
/// <param name="Field">The field to add.</param>
public sealed record AddCompositeField(SqlIdentifier SchemaName, SqlIdentifier TypeName, CompositeField Field) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
