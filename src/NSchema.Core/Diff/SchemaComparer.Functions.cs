using NSchema.Diff.Model;
using NSchema.Schema.Model.Functions;
using NSchema.Schema.Model.Schemas;

namespace NSchema.Diff;

internal sealed partial class SchemaComparer
{
    private static List<FunctionDiff> CompareFunctions(string schemaName, IReadOnlyList<Function> current, SchemaDefinition desired) =>
        CompareObjects(schemaName, "function", current, desired.Functions, desired.DroppedFunctions, desired.IsPartial,
            function => new FunctionDiff(schemaName, function.Name, ChangeKind.Remove),
            function => BuildNewFunction(schemaName, function),
            (currentFunction, desiredFunction) => BuildModifiedFunction(schemaName, currentFunction, desiredFunction));

    private static FunctionDiff BuildNewFunction(string schema, Function function) =>
        new(schema, function.Name, ChangeKind.Add, Definition: function,
            Comment: ValueChanges.Changed(null, function.Comment));

    // The arguments and definition are opaque, compared for cosmetic equivalence. A definition-only change is
    // a replace (the provider emits CREATE OR REPLACE); an argument change forces a drop + recreate, because a
    // replace under a different signature would create a separate overload rather than replacing — so the diff
    // carries the argument transition and the full desired definition rides along for the recreate.
    private static FunctionDiff? BuildModifiedFunction(string schema, Function current, Function desired)
    {
        var renamedFrom = current.Name == desired.Name ? null : current.Name;
        var argumentsChanged = !SqlTextNormalizer.AreEquivalent(current.Arguments, desired.Arguments);
        var definitionChanged = !SqlTextNormalizer.AreEquivalent(current.Definition, desired.Definition);
        var comment = ValueChanges.Changed(current.Comment, desired.Comment);

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
