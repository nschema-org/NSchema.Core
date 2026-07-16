using NSchema.Model;
namespace NSchema.Plan.Domain.Models.CompositeTypes;

/// <summary>
/// Represents the removal of an existing composite type.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the composite type to be removed.</param>
/// <param name="TypeName">The name of the composite type to be removed.</param>
public sealed record DropCompositeType(SqlIdentifier SchemaName, SqlIdentifier TypeName) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => true;
}
