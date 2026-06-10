using NSchema.Diff.Model;
using NSchema.Schema.Model;

namespace NSchema.Diff;

internal sealed partial class SchemaComparer
{
    private List<ViewDiff> CompareViews(string schemaName, IReadOnlyList<View> current, SchemaDefinition desired)
    {
        var result = new List<ViewDiff>();
        var droppedViews = desired.DroppedViews;
        var (forDesired, currentMatched) = MatchEntities(current, desired.Views, v => v.Name, v => v.OldName, "view", schemaName);

        for (var j = 0; j < current.Count; j++)
        {
            var currentView = current[j];
            if (currentMatched[j])
            {
                continue;
            }

            // A view absent from the desired set is dropped — unless the schema is partial and it was not named
            // in an explicit DROP VIEW, mirroring how unmanaged tables are left alone.
            if (droppedViews.Contains(currentView.Name, StringComparer.OrdinalIgnoreCase) || !desired.IsPartial)
            {
                result.Add(RemovedView(schemaName, currentView));
            }
        }

        for (var i = 0; i < desired.Views.Count; i++)
        {
            var desiredView = desired.Views[i];
            if (forDesired[i] is not { } matchingCurrent)
            {
                result.Add(BuildNewView(schemaName, desiredView));
            }
            else if (BuildModifiedView(schemaName, matchingCurrent, desiredView) is { } diff)
            {
                result.Add(diff);
            }
        }

        return result;
    }

    private static ViewDiff RemovedView(string schema, View view) =>
        new(schema, view.Name, ChangeKind.Remove, DependsOn: view.DependsOn);

    private static ViewDiff BuildNewView(string schema, View view) =>
        new(schema, view.Name, ChangeKind.Add, Definition: view,
            Comment: view.Comment is not null ? new ValueChange<string>(null, view.Comment) : null,
            DependsOn: view.DependsOn);

    // A view's body is opaque, so any textual change is a replace (the linearizer emits a fresh CreateView). A
    // rename and a comment change are tracked independently and may accompany a body change.
    private static ViewDiff? BuildModifiedView(string schema, View current, View desired)
    {
        var renamedFrom = current.Name == desired.Name ? null : current.Name;
        // Compare bodies for *equivalence*, not byte-equality, so a database's cosmetic re-emission
        // (whitespace, trailing terminator) does not read as a change. See SqlTextNormalizer.
        var bodyChanged = !SqlTextNormalizer.AreEquivalent(current.Body, desired.Body);
        var comment = current.Comment != desired.Comment ? new ValueChange<string>(current.Comment, desired.Comment) : null;

        if (renamedFrom is null && !bodyChanged && comment is null)
        {
            return null;
        }

        return new ViewDiff(schema, desired.Name, ChangeKind.Modify, renamedFrom,
            bodyChanged ? desired : null, comment, desired.DependsOn);
    }
}
