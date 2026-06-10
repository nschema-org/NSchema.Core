using NSchema.Diff.Model;
using NSchema.Schema.Model;

namespace NSchema.Diff;

internal sealed partial class SchemaComparer
{
    private static List<ViewDiff> CompareViews(string schemaName, IReadOnlyList<View> current, SchemaDefinition desired) =>
        CompareObjects(schemaName, "view", current, desired.Views, desired.DroppedViews, desired.IsPartial,
            view => RemovedView(schemaName, view),
            view => BuildNewView(schemaName, view),
            (currentView, desiredView) => BuildModifiedView(schemaName, currentView, desiredView));

    private static ViewDiff RemovedView(string schema, View view) =>
        new(schema, view.Name, ChangeKind.Remove, DependsOn: view.DependsOn);

    private static ViewDiff BuildNewView(string schema, View view) =>
        new(schema, view.Name, ChangeKind.Add, Definition: view,
            Comment: ValueChanges.Changed(null, view.Comment),
            DependsOn: view.DependsOn);

    // A view's body is opaque, so any textual change is a replace (the linearizer emits a fresh CreateView). A
    // rename and a comment change are tracked independently and may accompany a body change.
    private static ViewDiff? BuildModifiedView(string schema, View current, View desired)
    {
        var renamedFrom = current.Name == desired.Name ? null : current.Name;
        // Compare bodies for *equivalence*, not byte-equality, so a database's cosmetic re-emission
        // (whitespace, trailing terminator) does not read as a change. See SqlTextNormalizer.
        var bodyChanged = !SqlTextNormalizer.AreEquivalent(current.Body, desired.Body);
        var comment = ValueChanges.Changed(current.Comment, desired.Comment);

        if (renamedFrom is null && !bodyChanged && comment is null)
        {
            return null;
        }

        return new ViewDiff(schema, desired.Name, ChangeKind.Modify, renamedFrom,
            bodyChanged ? desired : null, comment, desired.DependsOn);
    }
}
