using NSchema.Diff.Model;
using NSchema.Schema.Model;

namespace NSchema.Diff;

internal sealed partial class SchemaComparer
{
    private static List<ProcedureDiff> CompareProcedures(string schemaName, IReadOnlyList<Procedure> current, SchemaDefinition desired) =>
        CompareObjects(schemaName, "procedure", current, desired.Procedures, desired.DroppedProcedures, desired.IsPartial,
            procedure => new ProcedureDiff(schemaName, procedure.Name, ChangeKind.Remove),
            procedure => BuildNewProcedure(schemaName, procedure),
            (currentProcedure, desiredProcedure) => BuildModifiedProcedure(schemaName, currentProcedure, desiredProcedure));

    private static ProcedureDiff BuildNewProcedure(string schema, Procedure procedure) =>
        new(schema, procedure.Name, ChangeKind.Add, Definition: procedure,
            Comment: ValueChanges.Changed(null, procedure.Comment));

    // Mirrors BuildModifiedFunction: definition-only change is a replace, an argument change forces a
    // drop + recreate (see SchemaComparer.Functions.cs).
    private static ProcedureDiff? BuildModifiedProcedure(string schema, Procedure current, Procedure desired)
    {
        var renamedFrom = current.Name == desired.Name ? null : current.Name;
        var argumentsChanged = !SqlTextNormalizer.AreEquivalent(current.Arguments, desired.Arguments);
        var definitionChanged = !SqlTextNormalizer.AreEquivalent(current.Definition, desired.Definition);
        var comment = ValueChanges.Changed(current.Comment, desired.Comment);

        if (renamedFrom is null && !argumentsChanged && !definitionChanged && comment is null)
        {
            return null;
        }

        return new ProcedureDiff(schema, desired.Name, ChangeKind.Modify, renamedFrom,
            Definition: argumentsChanged || definitionChanged ? desired : null,
            Arguments: argumentsChanged ? new ValueChange<string>(current.Arguments, desired.Arguments) : null,
            Comment: comment);
    }
}
