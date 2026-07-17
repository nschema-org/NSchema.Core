using NSchema.Diff.Model;
using NSchema.Model;
using NSchema.Model.Schemas;
using NSchema.Model.Views;
using NSchema.Project.Model.Directives;

namespace NSchema.Tests.Diff;

public partial class DatabaseComparerTests
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
        diff.DependsOn.ShouldHaveSingleItem().ShouldBe(new ViewDependency(new SqlIdentifier("app"), new SqlIdentifier("users")));
    }

    [Fact]
    public void Compare_RemovedView_IsRemove_AndCarriesCurrentDependencies()
    {
        // A removed view keeps its (current) dependencies so the planner can order the drop.
        var diff = DiffViews([View("active", "SELECT * FROM app.users")], []);

        diff!.Kind.ShouldBe(ChangeKind.Remove);
        diff.Definition.ShouldBeNull();
        diff.DependsOn.ShouldHaveSingleItem().ShouldBe(new ViewDependency(new SqlIdentifier("app"), new SqlIdentifier("users")));
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
            [View("active", "SELECT * FROM app.users")],
            new ProjectDirectives(Renames: [new ObjectRenameDirective(ObjectKind.View, App("legacy"), new SqlIdentifier("active"))]));

        diff!.Kind.ShouldBe(ChangeKind.Modify);
        diff.RenamedFrom.ShouldBe("legacy");
        diff.Name.ShouldBe("active");
        diff.Definition.ShouldBeNull(); // body unchanged, so it is a rename only
    }

    [Fact]
    public void Compare_UnchangedView_ProducesNoDiff()
        => DiffViews([View("active", "SELECT * FROM app.users")], [View("active", "SELECT * FROM app.users")]).ShouldBeNull();

    // A database stores a view's definition and re-emits it in its own canonical form. Insignificant
    // whitespace and a trailing terminator are cosmetic and must not read as drift, or a clean
    // import → plan would show a phantom change. (Semantic rewrites — qualification, * expansion,
    // injected casts — are reconciled provider-side and are out of scope here.)
    [Fact]
    public void Compare_ViewBodyDifferingOnlyInWhitespace_ProducesNoDiff()
        => DiffViews(
            [View("active", "SELECT id, name\n  FROM app.users\n  WHERE balance > 0")],
            [View("active", "SELECT id, name FROM app.users WHERE balance > 0")]).ShouldBeNull();

    [Fact]
    public void Compare_ViewBodyDifferingOnlyInTrailingSemicolon_ProducesNoDiff()
        => DiffViews(
            [View("active", "SELECT * FROM app.users;")],
            [View("active", "SELECT * FROM app.users")]).ShouldBeNull();

    // Whitespace *inside* a string literal is significant — normalization must not conflate the two,
    // or we would silently miss a real change.
    [Fact]
    public void Compare_ViewBodyWhitespaceInsideStringLiteral_IsSignificant()
        => DiffViews(
            [View("labelled", "SELECT 'a  b' AS label FROM app.users")],
            [View("labelled", "SELECT 'a b' AS label FROM app.users")])!.Kind.ShouldBe(ChangeKind.Modify);

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
        var diff = Compare(
            Db(new Schema(new SqlIdentifier("app"), Views: [View("active", "SELECT * FROM app.users")])),
            Db(new Schema(new SqlIdentifier("app"))),
            PartialApp());

        diff.Schemas.ShouldBeEmpty();
    }
}
