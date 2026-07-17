using NSchema.Diff.Model;
using NSchema.Diff.Model.Indexes;
using NSchema.Diff.Model.Schemas;
using NSchema.Diff.Model.Views;
using NSchema.Model;
using NSchema.Model.Indexes;
using NSchema.Model.Views;
using NSchema.Plan.Model;
using NSchema.Plan.Model.Indexes;
using NSchema.Plan.Model.Services;
using NSchema.Plan.Model.Views;

namespace NSchema.Tests.Plan;

/// <summary>
/// Pins how the linearizer turns materialized-view diffs into actions: a recreate is a drop + create (no
/// CREATE OR REPLACE MATERIALIZED VIEW), in-place index changes become index actions against the view, and the
/// materialized flag flows onto the view actions.
/// </summary>
public sealed class PlanLinearizerMaterializedViewTests
{
    private readonly PlanLinearizer _linearizer = new();

    private IReadOnlyList<MigrationAction> Linearize(ViewDiff view) =>
        _linearizer.Linearize(new DatabaseDiff([new SchemaDiff(new SqlIdentifier("app"), Views: [view])]));

    [Fact]
    public void RecreatedMaterializedView_EmitsDropAndCreateBothMaterialized()
    {
        var mv = new View(new SqlIdentifier("daily"), new SqlText("SELECT 2"), isMaterialized: true);
        var actions = Linearize(new ViewDiff(new SqlIdentifier("app"), new SqlIdentifier("daily"), ChangeKind.Modify,
            Definition: mv, IsMaterialized: true, RequiresRecreate: true));

        actions.OfType<DropView>().ShouldHaveSingleItem().IsMaterialized.ShouldBeTrue();
        actions.OfType<CreateView>().ShouldHaveSingleItem().View.IsMaterialized.ShouldBeTrue();
    }

    [Fact]
    public void RecreatedMaterializedView_DropsBeforeItCreates()
    {
        var mv = new View(new SqlIdentifier("daily"), new SqlText("SELECT 2"), isMaterialized: true);
        var actions = Linearize(new ViewDiff(new SqlIdentifier("app"), new SqlIdentifier("daily"), ChangeKind.Modify,
            Definition: mv, IsMaterialized: true, RequiresRecreate: true));

        var drop = actions.Select((a, i) => (a, i)).Single(x => x.a is DropView).i;
        var create = actions.Select((a, i) => (a, i)).Single(x => x.a is CreateView).i;
        drop.ShouldBeLessThan(create);
    }

    [Fact]
    public void InPlaceIndexChange_EmitsIndexActionsAgainstTheView()
    {
        var actions = Linearize(new ViewDiff(new SqlIdentifier("app"), new SqlIdentifier("daily"), ChangeKind.Modify, IsMaterialized: true,
            Indexes:
            [
                new IndexDiff(ChangeKind.Add, new SqlIdentifier("daily_ix"), new TableIndex(new SqlIdentifier("daily_ix"), ["x"])),
                new IndexDiff(ChangeKind.Remove, new SqlIdentifier("old_ix")),
            ]));

        actions.OfType<CreateIndex>().ShouldHaveSingleItem().TableName.ShouldBe("daily");
        actions.OfType<DropIndex>().ShouldHaveSingleItem().IndexName.ShouldBe("old_ix");
        actions.OfType<CreateView>().ShouldBeEmpty(); // body unchanged, no recreate
    }

    [Fact]
    public void RenamedMaterializedView_IndexDropTargetsOldName()
    {
        // The index drop sorts before RenameView, so it runs while the view still carries its old name; the
        // index create sorts after and targets the new one.
        var actions = Linearize(new ViewDiff(new SqlIdentifier("app"), new SqlIdentifier("daily"), ChangeKind.Modify, RenamedFrom: new SqlIdentifier("nightly"), IsMaterialized: true,
            Indexes:
            [
                new IndexDiff(ChangeKind.Add, new SqlIdentifier("daily_ix"), new TableIndex(new SqlIdentifier("daily_ix"), ["x"])),
                new IndexDiff(ChangeKind.Remove, new SqlIdentifier("old_ix")),
            ]));

        actions.OfType<DropIndex>().ShouldHaveSingleItem().TableName.ShouldBe("nightly");
        actions.OfType<CreateIndex>().ShouldHaveSingleItem().TableName.ShouldBe("daily");
        var dropIndex = actions.Select((a, i) => (a, i)).Single(x => x.a is DropIndex).i;
        var rename = actions.Select((a, i) => (a, i)).Single(x => x.a is RenameView).i;
        dropIndex.ShouldBeLessThan(rename);
    }

    [Fact]
    public void RenamedRecreatedView_DropsOldNameAndSkipsRename()
    {
        // A rename accompanying a recreate is subsumed by it: the old name is dropped and the definition
        // recreates the view under the new one.
        var mv = new View(new SqlIdentifier("daily"), new SqlText("SELECT 2"), isMaterialized: true);
        var actions = Linearize(new ViewDiff(new SqlIdentifier("app"), new SqlIdentifier("daily"), ChangeKind.Modify, RenamedFrom: new SqlIdentifier("nightly"),
            Definition: mv, IsMaterialized: true, RequiresRecreate: true));

        actions.OfType<RenameView>().ShouldBeEmpty();
        actions.OfType<DropView>().ShouldHaveSingleItem().ViewName.ShouldBe("nightly");
        actions.OfType<CreateView>().ShouldHaveSingleItem().View.Name.ShouldBe("daily");
    }

    [Fact]
    public void ViewToMaterializedFlip_DropsAsPlainAndCreatesAsMaterialized()
    {
        // The view being dropped is still the current (plain) one; only the recreate is materialized.
        var mv = new View(new SqlIdentifier("v"), new SqlText("SELECT 1"), isMaterialized: true);
        var actions = Linearize(new ViewDiff(new SqlIdentifier("app"), new SqlIdentifier("v"), ChangeKind.Modify,
            Definition: mv, IsMaterialized: true,
            Materialized: new ValueChange<bool>(false, true), RequiresRecreate: true));

        actions.OfType<DropView>().ShouldHaveSingleItem().IsMaterialized.ShouldBeFalse();
        actions.OfType<CreateView>().ShouldHaveSingleItem().View.IsMaterialized.ShouldBeTrue();
    }

    [Fact]
    public void MaterializedToViewFlip_DropsAsMaterialized()
    {
        var actions = Linearize(new ViewDiff(new SqlIdentifier("app"), new SqlIdentifier("v"), ChangeKind.Modify,
            Definition: new View(new SqlIdentifier("v"), new SqlText("SELECT 1")), IsMaterialized: false,
            Materialized: new ValueChange<bool>(true, false), RequiresRecreate: true));

        actions.OfType<DropView>().ShouldHaveSingleItem().IsMaterialized.ShouldBeTrue();
        actions.OfType<CreateView>().ShouldHaveSingleItem().View.IsMaterialized.ShouldBeFalse();
    }

    [Fact]
    public void PlainViewBodyChange_EmitsOnlyCreateNoDrop()
    {
        var actions = Linearize(new ViewDiff(new SqlIdentifier("app"), new SqlIdentifier("v"), ChangeKind.Modify, Definition: new View(new SqlIdentifier("v"), new SqlText("SELECT 2"))));

        actions.OfType<CreateView>().ShouldHaveSingleItem();
        actions.OfType<DropView>().ShouldBeEmpty();
    }
}
