using NSchema.Diff.Model;
using NSchema.Schema.Model;

namespace NSchema.Diff;

internal sealed partial class SchemaComparer
{
    private List<EnumDiff> CompareEnums(string schemaName, IReadOnlyList<EnumType> current, SchemaDefinition desired)
    {
        var result = new List<EnumDiff>();
        var droppedEnums = desired.DroppedEnums;
        var (forDesired, currentMatched) = MatchEntities(current, desired.Enums, e => e.Name, e => e.OldName, "enum", schemaName);

        for (var j = 0; j < current.Count; j++)
        {
            var currentEnum = current[j];
            if (currentMatched[j])
            {
                continue;
            }

            // An enum absent from the desired set is dropped — unless the schema is partial and it was not named
            // in an explicit DROP ENUM, mirroring how unmanaged tables are left alone.
            if (droppedEnums.Contains(currentEnum.Name, StringComparer.OrdinalIgnoreCase) || !desired.IsPartial)
            {
                result.Add(new EnumDiff(schemaName, currentEnum.Name, ChangeKind.Remove));
            }
        }

        for (var i = 0; i < desired.Enums.Count; i++)
        {
            var desiredEnum = desired.Enums[i];
            if (forDesired[i] is not { } matchingCurrent)
            {
                result.Add(BuildNewEnum(schemaName, desiredEnum));
            }
            else if (BuildModifiedEnum(schemaName, matchingCurrent, desiredEnum) is { } diff)
            {
                result.Add(diff);
            }
        }

        return result;
    }

    private static EnumDiff BuildNewEnum(string schema, EnumType enumType) =>
        new(schema, enumType.Name, ChangeKind.Add, Definition: enumType,
            Comment: enumType.Comment is not null ? new ValueChange<string>(null, enumType.Comment) : null);

    // Enum values are additions-only: a value-compatible change carries the anchored additions, while a removal
    // or reorder carries only the old/new value lists (AddedValues stays empty, so RequiresRecreate is true).
    // The diff still records the latter so drift can display it; planning it is rejected by policy.
    private static EnumDiff? BuildModifiedEnum(string schema, EnumType current, EnumType desired)
    {
        var renamedFrom = current.Name == desired.Name ? null : current.Name;
        var comment = current.Comment != desired.Comment ? new ValueChange<string>(current.Comment, desired.Comment) : null;

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
