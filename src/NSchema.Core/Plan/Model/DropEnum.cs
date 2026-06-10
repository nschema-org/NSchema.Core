namespace NSchema.Plan.Model;

/// <summary>
/// Represents the removal of an existing enum type from the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the enum to be removed.</param>
/// <param name="EnumName">The name of the enum to be removed.</param>
public sealed record DropEnum(string SchemaName, string EnumName) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => true;
}
