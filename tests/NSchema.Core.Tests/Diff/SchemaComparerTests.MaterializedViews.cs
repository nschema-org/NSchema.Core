using NSchema.Project.Domain.Models;
using NSchema.Diff.Domain.Models;
using NSchema.Project.Ddl;
using NSchema.Project.Domain.Models.Indexes;
using NSchema.Project.Domain.Models.Views;

namespace NSchema.Tests.Diff;

public partial class SchemaComparerTests
{
    // -------------------------------------------------------------------------
    // Materialized views (View with IsMaterialized + Indexes)
    // -------------------------------------------------------------------------

    private static View Matview(string name, string body, IReadOnlyList<TableIndex>? indexes = null, string? comment = null) =>
        new(new SqlIdentifier(name), body, null, comment, ViewDependencyExtractor.Extract(body, new SqlIdentifier("app")), IsMaterialized: true, Indexes: indexes);

    [Fact]
    public void Compare_NewMaterializedView_IsAddWithMaterializedFlag()
    {
        var diff = DiffViews([], [Matview("daily", "SELECT 1")]);

        diff!.Kind.ShouldBe(ChangeKind.Add);
        diff.IsMaterialized.ShouldBeTrue();
        diff.Definition!.IsMaterialized.ShouldBeTrue();
    }

    [Fact]
    public void Compare_MaterializedViewBodyChange_RequiresRecreate()
    {
        // There is no CREATE OR REPLACE MATERIALIZED VIEW, so a body change must drop + recreate.
        var diff = DiffViews([Matview("daily", "SELECT 1")], [Matview("daily", "SELECT 2")]);

        diff!.Kind.ShouldBe(ChangeKind.Modify);
        diff.RequiresRecreate.ShouldBeTrue();
        diff.Definition.ShouldNotBeNull();
    }

    [Fact]
    public void Compare_PlainViewBodyChange_DoesNotRequireRecreate()
    {
        var diff = DiffViews([View("v", "SELECT 1")], [View("v", "SELECT 2")]);

        diff!.RequiresRecreate.ShouldBeFalse();
        diff.Definition.ShouldNotBeNull(); // an in-place CREATE OR REPLACE
    }

    [Fact]
    public void Compare_ViewToMaterializedFlip_RequiresRecreate()
    {
        var diff = DiffViews([View("v", "SELECT 1")], [Matview("v", "SELECT 1")]);

        diff!.RequiresRecreate.ShouldBeTrue();
        diff.IsMaterialized.ShouldBeTrue();
        // The flip is carried explicitly so the plan can drop the view as what it currently is.
        diff.Materialized.ShouldNotBeNull()
            .ShouldSatisfyAllConditions(m => m.Old.ShouldBe(false), m => m.New.ShouldBe(true));
    }

    [Fact]
    public void Compare_MaterializedViewBodyChange_DoesNotReportMaterializedFlip()
    {
        var diff = DiffViews([Matview("daily", "SELECT 1")], [Matview("daily", "SELECT 2")]);

        diff!.Materialized.ShouldBeNull();
    }

    [Fact]
    public void Compare_MaterializedViewIndexAdded_IsInPlaceIndexDiff()
    {
        var diff = DiffViews(
            [Matview("daily", "SELECT 1")],
            [Matview("daily", "SELECT 1", indexes: [new TableIndex(new SqlIdentifier("daily_ix"), ["x"])])]);

        diff!.RequiresRecreate.ShouldBeFalse();
        diff.Definition.ShouldBeNull(); // body unchanged
        diff.Indexes.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Add);
    }

    [Fact]
    public void Compare_MaterializedViewBodyAndIndexChange_RecreatesWithIndexesOnDefinition()
    {
        var diff = DiffViews(
            [Matview("daily", "SELECT 1", indexes: [new TableIndex(new SqlIdentifier("a"), ["x"])])],
            [Matview("daily", "SELECT 2", indexes: [new TableIndex(new SqlIdentifier("b"), ["y"])])]);

        diff!.RequiresRecreate.ShouldBeTrue();
        diff.Indexes.ShouldBeEmpty(); // not diffed in place during a recreate
        diff.Definition!.Indexes.ShouldHaveSingleItem().Name.ShouldBe("b"); // rebuilt with the definition
    }

    [Fact]
    public void Compare_RemovedMaterializedView_CarriesMaterializedFlag()
    {
        var diff = DiffViews([Matview("daily", "SELECT 1")], []);

        diff!.Kind.ShouldBe(ChangeKind.Remove);
        diff.IsMaterialized.ShouldBeTrue();
    }
}
