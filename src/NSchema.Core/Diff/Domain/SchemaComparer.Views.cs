using NSchema.Diff.Domain.Models;
using NSchema.Diff.Domain.Models.Indexes;
using NSchema.Diff.Domain.Models.Views;
using NSchema.Project.Domain.Models.Schemas;
using NSchema.Project.Domain.Models.Views;

namespace NSchema.Diff.Domain;

internal sealed partial class SchemaComparer
{
    private List<ViewDiff> CompareViews(string schemaName, IReadOnlyList<View> current, SchemaDefinition desired) =>
        CompareObjects(schemaName, "view", current, desired.Views, desired.DroppedViews, desired.IsPartial,
            view => RemovedView(schemaName, view),
            view => BuildNewView(schemaName, view),
            (currentView, desiredView) => BuildModifiedView(schemaName, currentView, desiredView));

    private static ViewDiff RemovedView(string schema, View view) =>
        new(schema, view.Name, ChangeKind.Remove, DependsOn: view.DependsOn, IsMaterialized: view.IsMaterialized);

    private static ViewDiff BuildNewView(string schema, View view) =>
        new(schema, view.Name, ChangeKind.Add, Definition: view,
            Comment: ValueChanges.Changed(null, view.Comment),
            DependsOn: view.DependsOn, IsMaterialized: view.IsMaterialized);

    // A view's body is opaque, so any textual change is a replace. For a plain view that replace is in place
    // (CREATE OR REPLACE VIEW); for a materialized view it must be a drop + recreate (there is no
    // CREATE OR REPLACE MATERIALIZED VIEW), as must a view ⇄ materialized-view conversion. A rename, comment
    // change, and a materialized view's index changes are tracked independently and may accompany the rest.
    private ViewDiff? BuildModifiedView(string schema, View current, View desired)
    {
        var renamedFrom = current.Name == desired.Name ? null : current.Name;
        // Compare bodies for *equivalence*, not byte-equality, so a database's cosmetic re-emission
        // (whitespace, trailing terminator) does not read as a change. See SqlTextNormalizer.
        var bodyChanged = !SqlTextNormalizer.AreEquivalent(current.Body, desired.Body);
        var comment = ValueChanges.Changed(current.Comment, desired.Comment);
        var materializationFlipped = current.IsMaterialized != desired.IsMaterialized;

        var requiresRecreate = materializationFlipped || (bodyChanged && desired.IsMaterialized);

        // Indexes are diffed in place only when the materialized view is not being recreated; on a create or
        // recreate they ride along on the definition and are rebuilt with it.
        IReadOnlyList<IndexDiff> indexes = requiresRecreate
            ? []
            : CompareTableMembers(schema, desired.Name, "Index", current.Indexes, desired.Indexes,
                (kind, name, definition, indexComment) => new IndexDiff(kind, name, definition, indexComment));

        // The definition is carried whenever the body must be (re)written: a recreate, or a plain-view replace.
        var carryDefinition = requiresRecreate || (bodyChanged && !desired.IsMaterialized);

        if (renamedFrom is null && !bodyChanged && comment is null && !materializationFlipped && indexes.Count == 0)
        {
            return null;
        }

        return new ViewDiff(schema, desired.Name, ChangeKind.Modify, renamedFrom,
            carryDefinition ? desired : null, comment, desired.DependsOn,
            IsMaterialized: desired.IsMaterialized,
            Materialized: materializationFlipped ? new ValueChange<bool>(current.IsMaterialized, desired.IsMaterialized) : null,
            RequiresRecreate: requiresRecreate, Indexes: indexes);
    }
}
