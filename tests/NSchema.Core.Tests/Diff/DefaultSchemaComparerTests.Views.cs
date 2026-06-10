using NSchema.Diff.Model;
using NSchema.Schema.Model;

namespace NSchema.Tests.Diff;

public partial class SchemaComparerTests
{
    // -------------------------------------------------------------------------
    // Views
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_NewView_IsAddCarryingDefinition()
    {
        var diff = DiffViews([], [View("active", "SELECT * FROM app.users")]);

        diff!.Kind.ShouldBe(ChangeKind.Add);
        diff.Definition.ShouldNotBeNull();
        diff.DependsOn.ShouldHaveSingleItem().ShouldBe(new ViewDependency("app", "users"));
    }

    [Fact]
    public void Compare_RemovedView_IsRemove_AndCarriesCurrentDependencies()
    {
        // A removed view keeps its (current) dependencies so the planner can order the drop.
        var diff = DiffViews([View("active", "SELECT * FROM app.users")], []);

        diff!.Kind.ShouldBe(ChangeKind.Remove);
        diff.Definition.ShouldBeNull();
        diff.DependsOn.ShouldHaveSingleItem().ShouldBe(new ViewDependency("app", "users"));
    }

    [Fact]
    public void Compare_ViewBodyChange_IsModifyCarryingNewDefinition()
    {
        var diff = DiffViews(
            [View("active", "SELECT id FROM app.users")],
            [View("active", "SELECT id, name FROM app.users")]);

        diff!.Kind.ShouldBe(ChangeKind.Modify);
        diff.Definition!.Body.ShouldBe("SELECT id, name FROM app.users"); // replace
        diff.RenamedFrom.ShouldBeNull();
    }

    [Fact]
    public void Compare_ViewCommentOnlyChange_IsModifyWithoutDefinition()
    {
        var diff = DiffViews(
            [View("active", "SELECT * FROM app.users", comment: "old")],
            [View("active", "SELECT * FROM app.users", comment: "new")]);

        diff!.Kind.ShouldBe(ChangeKind.Modify);
        diff.Definition.ShouldBeNull(); // no body change -> no replace
        diff.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }

    [Fact]
    public void Compare_RenamedView_SetsRenamedFrom()
    {
        var diff = DiffViews(
            [View("legacy", "SELECT * FROM app.users")],
            [View("active", "SELECT * FROM app.users", oldName: "legacy")]);

        diff!.Kind.ShouldBe(ChangeKind.Modify);
        diff.RenamedFrom.ShouldBe("legacy");
        diff.Name.ShouldBe("active");
        diff.Definition.ShouldBeNull(); // body unchanged, so it is a rename only
    }

    [Fact]
    public void Compare_UnchangedView_ProducesNoDiff()
        => DiffViews([View("active", "SELECT * FROM app.users")], [View("active", "SELECT * FROM app.users")]).ShouldBeNull();

    [Fact]
    public void Compare_ViewDependencies_AreDerivedFromTheBodyThroughTheComparer()
    {
        // The comparer surfaces every FROM/JOIN target the extractor finds.
        var diff = DiffViews([], [View("report", "SELECT * FROM app.orders o JOIN app.customers c ON o.cid = c.id")]);

        diff!.DependsOn.Select(d => $"{d.Schema}.{d.Name}").ShouldBe(["app.orders", "app.customers"]);
    }

    [Fact]
    public void Compare_PartialSchema_LeavesUnmanagedViewAlone()
    {
        // The view exists in the database but isn't declared; a partial schema must not drop it.
        var diff = _sut.Compare(
            Db(new SchemaDefinition("app", Views: [View("active", "SELECT * FROM app.users")])),
            Db(new SchemaDefinition("app", IsPartial: true)));

        diff.Schemas.ShouldBeEmpty();
    }
}
