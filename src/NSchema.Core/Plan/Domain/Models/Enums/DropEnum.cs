using NSchema.Project.Domain.Models;
namespace NSchema.Plan.Domain.Models.Enums;

/// <summary>
/// Represents the removal of an existing enum type from the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the enum to be removed.</param>
/// <param name="EnumName">The name of the enum to be removed.</param>
public sealed record DropEnum(SqlIdentifier SchemaName, SqlIdentifier EnumName) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => true;
}
