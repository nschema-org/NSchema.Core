namespace NSchema.Plan.Model.CompositeTypes;

/// <summary>
/// Represents dropping a field from a composite type (<c>ALTER TYPE … DROP ATTRIBUTE …</c>).
/// </summary>
/// <param name="SchemaName">The name of the schema containing the composite type.</param>
/// <param name="TypeName">The name of the composite type.</param>
/// <param name="FieldName">The name of the field to drop.</param>
public sealed record DropCompositeField(string SchemaName, string TypeName, string FieldName) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => true;
}
