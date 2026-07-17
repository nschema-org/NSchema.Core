using NSchema.Diff.Model.Enums;
using NSchema.Model;
using NSchema.Model.Enums;
using NSchema.Model.Schemas;

namespace NSchema.Diff.Model.Services;

internal sealed partial class DatabaseComparer
{
    private static List<EnumDiff> CompareEnums(SqlIdentifier schemaName, SqlIdentifier currentSchemaName, IReadOnlyList<EnumType> current, Schema desired, DirectiveLookup directives) =>
        CompareObjects(schemaName, "enum", current, desired.Enums,
            directives.Renames(ObjectKind.Enum, currentSchemaName), directives.Drops(ObjectKind.Enum, currentSchemaName), directives.IsPartial(schemaName),
            enumType => new EnumDiff(schemaName, enumType.Name, ChangeKind.Remove),
            enumType => BuildNewEnum(schemaName, enumType),
            (currentEnum, desiredEnum) => BuildModifiedEnum(schemaName, currentEnum, desiredEnum));

    private static EnumDiff BuildNewEnum(SqlIdentifier schema, EnumType enumType) =>
        new(schema, enumType.Name, ChangeKind.Add, Definition: enumType,
            Comment: ValueChanges.Changed(null, enumType.Comment));

    // Enum values are additions-only: a value-compatible change carries the anchored additions, while a removal
    // or reorder carries only the old/new value lists (AddedValues stays empty, so RequiresRecreate is true).
    // The diff still records the latter so drift can display it; planning it is rejected by policy.
    private static EnumDiff? BuildModifiedEnum(SqlIdentifier schema, EnumType current, EnumType desired)
    {
        var renamedFrom = current.Name == desired.Name ? (SqlIdentifier?)null : current.Name;
        var comment = ValueChanges.Changed(current.Comment, desired.Comment);

        ValueChange<IReadOnlyList<string>>? values = null;
        List<EnumValueAddition>? additions = null;
        if (!current.Values.SequenceEqual(desired.Values, StringComparer.Ordinal))
        {
            values = new ValueChange<IReadOnlyList<string>>(current.Values, desired.Values);
            additions = ComputeValueAdditions(current.Values, desired.Values);
        }

        if (renamedFrom is null && values is null && comment is null)
        {
            return null;
        }

        return new EnumDiff(schema, desired.Name, ChangeKind.Modify, renamedFrom, null, additions, values, comment);
    }

    /// <summary>
    /// Expresses the change from <paramref name="current"/> to <paramref name="desired"/> as anchored value
    /// additions, or returns <see langword="null"/> when it cannot be (a value was removed or reordered).
    /// Greedy two-pointer subsequence matching is exact here because values are unique within an enum.
    /// </summary>
    private static List<EnumValueAddition>? ComputeValueAdditions(IReadOnlyList<string> current, IReadOnlyList<string> desired)
    {
        var isNew = new bool[desired.Count];
        var c = 0;
        for (var d = 0; d < desired.Count; d++)
        {
            if (c < current.Count && string.Equals(desired[d], current[c], StringComparison.Ordinal))
            {
                c++;
            }
            else
            {
                isNew[d] = true;
            }
        }

        if (c != current.Count)
        {
            return null;
        }

        // Additions execute in list order, so an After anchor always exists when it runs: it is either a
        // pre-existing value or was added by the previous addition. A run of new values at the head anchors
        // Before the first pre-existing value instead.
        var additions = new List<EnumValueAddition>();
        for (var d = 0; d < desired.Count; d++)
        {
            if (!isNew[d])
            {
                continue;
            }

            additions.Add(d > 0
                ? new EnumValueAddition(desired[d], After: desired[d - 1])
                : current.Count > 0
                    ? new EnumValueAddition(desired[d], Before: current[0])
                    : new EnumValueAddition(desired[d]));
        }
        return additions;
    }
}
