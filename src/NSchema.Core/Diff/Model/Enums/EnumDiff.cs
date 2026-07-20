using NSchema.Model;
using NSchema.Model.Enums;

namespace NSchema.Diff.Model.Enums;

/// <summary>
/// Describes a change to an enum type.
/// </summary>
/// <param name="Schema">The name of the schema the enum belongs to.</param>
/// <param name="Name">The enum name.</param>
/// <param name="Kind">The change to the enum.</param>
/// <param name="RenamedFrom">The previous enum name when the enum is being renamed; otherwise <see langword="null"/>.</param>
/// <param name="Definition">The enum definition for an added enum; otherwise <see langword="null"/>.</param>
/// <param name="AddedValues">The values being added for a value-compatible modification, in execution order.</param>
/// <param name="Values">The change to the value list, set whenever it changed at all (including changes that
/// cannot be planned), so drift can display it.</param>
/// <param name="Comment">The change to the enum's comment, if any.</param>
public sealed record EnumDiff(
    SqlIdentifier Schema,
    SqlIdentifier Name,
    ChangeKind Kind,
    SqlIdentifier? RenamedFrom = null,
    EnumType? Definition = null,
    IReadOnlyList<EnumValueAddition>? AddedValues = null,
    ValueChange<IReadOnlyList<EnumLabel>>? Values = null,
    ValueChange<string>? Comment = null
) : ISchemaObjectDiff
{
    /// <summary>
    /// The values being added for a value-compatible modification, in execution order.
    /// </summary>
    public IReadOnlyList<EnumValueAddition> AddedValues { get; init; } = AddedValues ?? [];

    /// <summary>
    /// The value list changed but cannot be expressed as additions — a value was removed or reordered. Planning
    /// such a change is rejected; the type must be recreated manually.
    /// </summary>
    public bool RequiresRecreate => Values is not null && AddedValues.Count == 0;
}
