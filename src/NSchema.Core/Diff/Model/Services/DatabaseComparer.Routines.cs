using NSchema.Diff.Model.Routines;
using NSchema.Model;
using NSchema.Model.Routines;
using NSchema.Model.Schemas;

namespace NSchema.Diff.Model.Services;

internal sealed partial class DatabaseComparer
{
    private static List<RoutineDiff> CompareRoutines(SqlIdentifier schemaName, SqlIdentifier currentSchemaName, IReadOnlyList<Routine> current, Schema desired, DirectiveLookup directives) =>
        CompareObjects(schemaName, "routine", current, desired.Routines,
            directives.Renames(ObjectKind.Routine, currentSchemaName), directives.Drops(ObjectKind.Routine, currentSchemaName), directives.IsPartial(schemaName),
            routine => new RoutineDiff(schemaName, routine.Name, ChangeKind.Remove, routine.Kind),
            routine => BuildNewRoutine(schemaName, routine),
            (currentRoutine, desiredRoutine) => BuildModifiedRoutine(schemaName, currentRoutine, desiredRoutine));

    private static RoutineDiff BuildNewRoutine(SqlIdentifier schema, Routine routine) =>
        new(schema, routine.Name, ChangeKind.Add, routine.Kind, Definition: routine,
            Comment: ValueChanges.Changed(null, routine.Comment));

    // The arguments and definition are opaque, compared for cosmetic equivalence. A definition-only change is
    // a replace (the provider emits CREATE OR REPLACE); an argument change forces a drop + recreate, because a
    // replace under a different signature would create a separate overload rather than replacing — so the diff
    // carries the argument transition and the full desired definition rides along for the recreate.
    private static RoutineDiff? BuildModifiedRoutine(SqlIdentifier schema, Routine current, Routine desired)
    {
        var renamedFrom = current.Name == desired.Name ? (SqlIdentifier?)null : current.Name;
        var argumentsChanged = !SqlTextNormalizer.AreEquivalent(current.Arguments, desired.Arguments);
        var definitionChanged = !SqlTextNormalizer.AreEquivalent(current.Definition, desired.Definition);
        var comment = ValueChanges.Changed(current.Comment, desired.Comment);

        // A kind change (function ⇄ procedure under the same name) also forces a recreate; it surfaces as an
        // argument change so the diff carries the transition and recreates.
        var kindChanged = current.Kind != desired.Kind;

        if (renamedFrom is null && !argumentsChanged && !definitionChanged && comment is null && !kindChanged)
        {
            return null;
        }

        var recreate = argumentsChanged || kindChanged;
        return new RoutineDiff(schema, desired.Name, ChangeKind.Modify, desired.Kind, renamedFrom,
            Definition: recreate || definitionChanged ? desired : null,
            Arguments: recreate ? new ValueChange<SqlText>(current.Arguments, desired.Arguments) : null,
            Comment: comment);
    }
}
