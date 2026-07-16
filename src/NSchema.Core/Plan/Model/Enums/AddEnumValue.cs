using NSchema.Model;
namespace NSchema.Plan.Domain.Models.Enums;

/// <summary>
/// Represents adding one value to an existing enum type, optionally anchored to a neighbouring value. At most
/// one anchor is set; with neither, the value appends to the end.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the enum.</param>
/// <param name="EnumName">The name of the enum.</param>
/// <param name="Value">The value to add.</param>
/// <param name="Before">Add the value before this existing value, when set.</param>
/// <param name="After">Add the value after this existing value, when set.</param>
public sealed record AddEnumValue(
    SqlIdentifier SchemaName,
    SqlIdentifier EnumName,
    string Value,
    string? Before = null,
    string? After = null
) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
