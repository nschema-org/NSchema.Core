using NSchema.Diff.Model;
using NSchema.Schema.Model;

namespace NSchema.Diff;

internal sealed partial class SchemaComparer
{
    private List<FunctionDiff> CompareFunctions(string schemaName, IReadOnlyList<Function> current, SchemaDefinition desired)
    {
        var result = new List<FunctionDiff>();
        var droppedFunctions = desired.DroppedFunctions;
        var (forDesired, currentMatched) = MatchEntities(current, desired.Functions, f => f.Name, f => f.OldName, "function", schemaName);

        for (var j = 0; j < current.Count; j++)
        {
            var currentFunction = current[j];
            if (currentMatched[j])
            {
                continue;
            }

            // A function absent from the desired set is dropped — unless the schema is partial and it was not
            // named in an explicit DROP FUNCTION, mirroring how unmanaged tables are left alone.
            if (droppedFunctions.Contains(currentFunction.Name, StringComparer.OrdinalIgnoreCase) || !desired.IsPartial)
            {
                result.Add(new FunctionDiff(schemaName, currentFunction.Name, ChangeKind.Remove));
            }
        }

        for (var i = 0; i < desired.Functions.Count; i++)
        {
            var desiredFunction = desired.Functions[i];
            if (forDesired[i] is not { } matchingCurrent)
            {
                result.Add(BuildNewFunction(schemaName, desiredFunction));
            }
            else if (BuildModifiedFunction(schemaName, matchingCurrent, desiredFunction) is { } diff)
            {
                result.Add(diff);
            }
        }

        return result;
    }

    private static FunctionDiff BuildNewFunction(string schema, Function function) =>
        new(schema, function.Name, ChangeKind.Add, Definition: function,
            Comment: function.Comment is not null ? new ValueChange<string>(null, function.Comment) : null);

    // The arguments and definition are opaque, compared for cosmetic equivalence. A definition-only change is
    // a replace (the provider emits CREATE OR REPLACE); an argument change forces a drop + recreate, because a
    // replace under a different signature would create a separate overload rather than replacing — so the diff
    // carries the argument transition and the full desired definition rides along for the recreate.
    private static FunctionDiff? BuildModifiedFunction(string schema, Function current, Function desired)
    {
        var renamedFrom = current.Name == desired.Name ? null : current.Name;
        var argumentsChanged = !SqlTextNormalizer.AreEquivalent(current.Arguments, desired.Arguments);
        var definitionChanged = !SqlTextNormalizer.AreEquivalent(current.Definition, desired.Definition);
        var comment = current.Comment != desired.Comment ? new ValueChange<string>(current.Comment, desired.Comment) : null;

        if (renamedFrom is null && !argumentsChanged && !definitionChanged && comment is null)
        {
            return null;
        }

        return new FunctionDiff(schema, desired.Name, ChangeKind.Modify, renamedFrom,
            Definition: argumentsChanged || definitionChanged ? desired : null,
            Arguments: argumentsChanged ? new ValueChange<string>(current.Arguments, desired.Arguments) : null,
            Comment: comment);
    }
}
