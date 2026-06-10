using NSchema.Diff.Model;
using NSchema.Schema.Model;

namespace NSchema.Diff;

internal sealed partial class SchemaComparer
{
    private List<ProcedureDiff> CompareProcedures(string schemaName, IReadOnlyList<Procedure> current, SchemaDefinition desired)
    {
        var result = new List<ProcedureDiff>();
        var droppedProcedures = desired.DroppedProcedures;
        var (forDesired, currentMatched) = MatchEntities(current, desired.Procedures, p => p.Name, p => p.OldName, "procedure", schemaName);

        for (var j = 0; j < current.Count; j++)
        {
            var currentProcedure = current[j];
            if (currentMatched[j])
            {
                continue;
            }

            // A procedure absent from the desired set is dropped — unless the schema is partial and it was not
            // named in an explicit DROP PROCEDURE, mirroring how unmanaged tables are left alone.
            if (droppedProcedures.Contains(currentProcedure.Name, StringComparer.OrdinalIgnoreCase) || !desired.IsPartial)
            {
                result.Add(new ProcedureDiff(schemaName, currentProcedure.Name, ChangeKind.Remove));
            }
        }

        for (var i = 0; i < desired.Procedures.Count; i++)
        {
            var desiredProcedure = desired.Procedures[i];
            if (forDesired[i] is not { } matchingCurrent)
            {
                result.Add(BuildNewProcedure(schemaName, desiredProcedure));
            }
            else if (BuildModifiedProcedure(schemaName, matchingCurrent, desiredProcedure) is { } diff)
            {
                result.Add(diff);
            }
        }

        return result;
    }

    private static ProcedureDiff BuildNewProcedure(string schema, Procedure procedure) =>
        new(schema, procedure.Name, ChangeKind.Add, Definition: procedure,
            Comment: procedure.Comment is not null ? new ValueChange<string>(null, procedure.Comment) : null);

    // Mirrors BuildModifiedFunction: definition-only change is a replace, an argument change forces a
    // drop + recreate (see SchemaComparer.Functions.cs).
    private static ProcedureDiff? BuildModifiedProcedure(string schema, Procedure current, Procedure desired)
    {
        var renamedFrom = current.Name == desired.Name ? null : current.Name;
        var argumentsChanged = !SqlTextNormalizer.AreEquivalent(current.Arguments, desired.Arguments);
        var definitionChanged = !SqlTextNormalizer.AreEquivalent(current.Definition, desired.Definition);
        var comment = current.Comment != desired.Comment ? new ValueChange<string>(current.Comment, desired.Comment) : null;

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
