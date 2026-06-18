using NSchema.Diff.Model;
using NSchema.Schema.Ddl;
using NSchema.Schema.Model.Indexes;
using NSchema.Schema.Model.Views;

namespace NSchema.Tests.Diff;

public partial class SchemaComparerTests
{
    // -------------------------------------------------------------------------
    // Materialized views (View with IsMaterialized + Indexes)
    // -------------------------------------------------------------------------

    private static View Matview(string name, string body, IReadOnlyList<TableIndex>? indexes = null, string? comment = null) =>
        new(name, body, null, comment, ViewDependencyExtractor.Extract(body, "app"), IsMaterialized: true, Indexes: indexes);

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
    }

    [Fact]
    public void Compare_MaterializedViewIndexAdded_IsInPlaceIndexDiff()
    {
        var diff = DiffViews(
            [Matview("daily", "SELECT 1")],
            [Matview("daily", "SELECT 1", indexes: [new TableIndex("daily_ix", ["x"])])]);

        diff!.RequiresRecreate.ShouldBeFalse();
        diff.Definition.ShouldBeNull(); // body unchanged
        diff.Indexes.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Add);
    }

    [Fact]
    public void Compare_MaterializedViewBodyAndIndexChange_RecreatesWithIndexesOnDefinition()
    {
        var diff = DiffViews(
            [Matview("daily", "SELECT 1", indexes: [new TableIndex("a", ["x"])])],
            [Matview("daily", "SELECT 2", indexes: [new TableIndex("b", ["y"])])]);

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
