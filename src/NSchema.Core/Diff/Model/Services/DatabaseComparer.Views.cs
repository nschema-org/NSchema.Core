using NSchema.Diff.Model.Indexes;
using NSchema.Diff.Model.Views;
using NSchema.Model;
using NSchema.Model.Schemas;
using NSchema.Model.Views;

namespace NSchema.Diff.Model.Services;

internal sealed partial class DatabaseComparer
{
    private List<ViewDiff> CompareViews(SqlIdentifier schemaName, IReadOnlyList<View> current, Schema desired, RenameLog renames) =>
        CompareObjects(current, desired.Views,
            name => renames.RenamedFrom(new ObjectIdentity(ObjectKind.View, schemaName, name)),
            view => RemovedView(schemaName, view),
            view => BuildNewView(schemaName, view),
            (currentView, desiredView, renamedFrom) => BuildModifiedView(schemaName, currentView, desiredView, renamedFrom));

    private static ViewDiff RemovedView(SqlIdentifier schema, View view) =>
        new(schema, view.Name, ChangeKind.Remove, DependsOn: view.DependsOn, IsMaterialized: view.IsMaterialized);

    private static ViewDiff BuildNewView(SqlIdentifier schema, View view) =>
        new(schema, view.Name, ChangeKind.Add, Definition: view,
            Comment: ValueChange.Between(null, view.Comment),
            DependsOn: view.DependsOn, IsMaterialized: view.IsMaterialized);

    // A view's body is opaque, so any textual change is a replace. For a plain view that replace is in place
    // (CREATE OR REPLACE VIEW); for a materialized view it must be a drop + recreate (there is no
    // CREATE OR REPLACE MATERIALIZED VIEW), as must a view ⇄ materialized-view conversion. A rename, comment
    // change, and a materialized view's index changes are tracked independently and may accompany the rest.
    private ViewDiff? BuildModifiedView(SqlIdentifier schema, View current, View desired, SqlIdentifier? renamedFrom)
    {
        // Compare bodies for *equivalence*, not byte-equality, so a database's cosmetic re-emission
        // (whitespace, trailing terminator) does not read as a change.
        var bodyChanged = !current.Body.EquivalentTo(desired.Body);
        var comment = ValueChange.Between(current.Comment, desired.Comment);
        var materializationFlipped = current.IsMaterialized != desired.IsMaterialized;

        var requiresRecreate = materializationFlipped || (bodyChanged && desired.IsMaterialized);

        // Indexes are diffed in place only when the materialized view is not being recreated; on a create or
        // recreate they ride along on the definition and are rebuilt with it.
        IReadOnlyList<IndexDiff> indexes = requiresRecreate
            ? []
            : CompareTableMembers(new ObjectAddress(schema, desired.Name), "Index", current.Indexes, desired.Indexes,
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
