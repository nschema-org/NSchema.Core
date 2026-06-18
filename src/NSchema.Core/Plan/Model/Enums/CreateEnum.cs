using NSchema.Schema.Model.Enums;

namespace NSchema.Plan.Model.Enums;

/// <summary>
/// Represents the creation of an enum type.
/// </summary>
/// <param name="SchemaName">The name of the schema the enum belongs to.</param>
/// <param name="Enum">The definition of the enum type to create.</param>
public sealed record CreateEnum(string SchemaName, EnumType Enum) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
