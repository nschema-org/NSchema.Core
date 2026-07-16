using NSchema.Model;
using NSchema.Model.CompositeTypes;

namespace NSchema.Diff.Domain.Models.CompositeTypes;

/// <summary>
/// Describes a change to a composite type.
/// </summary>
/// <param name="Schema">The name of the schema the composite type belongs to.</param>
/// <param name="Name">The composite type name.</param>
/// <param name="Kind">The change to the composite type.</param>
/// <param name="RenamedFrom">The previous name when the type is being renamed; otherwise <see langword="null"/>.</param>
/// <param name="Definition">The full definition for an added type (its fields are created inline); otherwise <see langword="null"/>.</param>
/// <param name="Fields">In-place field changes (added/dropped/retyped via <c>ALTER TYPE</c>) on an existing type.</param>
/// <param name="Comment">The change to the type's comment, if any.</param>
public sealed record CompositeTypeDiff(
    SqlIdentifier Schema,
    SqlIdentifier Name,
    ChangeKind Kind,
    SqlIdentifier? RenamedFrom = null,
    CompositeType? Definition = null,
    IReadOnlyList<CompositeFieldDiff>? Fields = null,
    ValueChange<string>? Comment = null
) : ISchemaObjectDiff
{
    /// <summary>
    /// In-place field changes on an existing composite type; empty on an add (the fields ride on the definition).
    /// </summary>
    public IReadOnlyList<CompositeFieldDiff> Fields { get; init; } = Fields ?? [];
}
