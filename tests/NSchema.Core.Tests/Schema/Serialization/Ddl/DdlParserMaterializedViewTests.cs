using NSchema.Schema.Ddl;
using NSchema.Schema.Model.Views;

namespace NSchema.Tests.Schema.Serialization.Ddl;

/// <summary>
/// Parser coverage for <c>CREATE MATERIALIZED VIEW</c> and the standalone <c>CREATE [UNIQUE] INDEX … ON s.v</c>
/// that attaches to a materialized view at build time (like a trigger attaches to its table).
/// </summary>
public sealed class DdlParserMaterializedViewTests
{
    private static View ParseView(string sql) =>
        new DdlParser("CREATE SCHEMA app; " + sql).Parse().Document.Schema
            .Schemas.ShouldHaveSingleItem().Views.ShouldHaveSingleItem();

    [Fact]
    public void Parse_MaterializedView_SetsFlag()
    {
        var view = ParseView("CREATE MATERIALIZED VIEW app.daily AS SELECT 1;");
        view.Name.ShouldBe("daily");
        view.IsMaterialized.ShouldBeTrue();
        view.Body.ShouldBe("SELECT 1");
    }

    [Fact]
    public void Parse_PlainView_IsNotMaterialized()
        => ParseView("CREATE VIEW app.v AS SELECT 1;").IsMaterialized.ShouldBeFalse();

    [Fact]
    public void Parse_StandaloneIndexOnMaterializedView_Attaches()
    {
        var view = ParseView(
            "CREATE MATERIALIZED VIEW app.daily AS SELECT date FROM app.t; CREATE INDEX daily_ix ON app.daily (date);");
        var index = view.Indexes.ShouldHaveSingleItem();
        index.Name.ShouldBe("daily_ix");
        index.Columns.Select(c => c.Expression).ShouldBe(["date"]);
        index.IsUnique.ShouldBeFalse();
    }

    [Fact]
    public void Parse_UniqueIndexWithPredicate_OnMaterializedView()
    {
        var index = ParseView(
            "CREATE MATERIALIZED VIEW app.daily AS SELECT date FROM app.t; " +
            "CREATE UNIQUE INDEX daily_ix ON app.daily (date) WHERE (date IS NOT NULL);").Indexes.ShouldHaveSingleItem();
        index.IsUnique.ShouldBeTrue();
        index.Predicate.ShouldBe("date IS NOT NULL");
    }

    [Fact]
    public void Parse_IndexBeforeItsMaterializedView_StillAttaches()
        // Build-time resolution: the index may be declared before the matview it targets.
        => ParseView("CREATE INDEX daily_ix ON app.daily (x); CREATE MATERIALIZED VIEW app.daily AS SELECT x FROM app.t;")
            .Indexes.ShouldHaveSingleItem().Name.ShouldBe("daily_ix");

    [Fact]
    public void Parse_IndexOnPlainView_Throws()
        => Should.Throw<DdlSyntaxException>(() =>
            new DdlParser("CREATE SCHEMA app; CREATE VIEW app.v AS SELECT 1; CREATE INDEX ix ON app.v (x);").Parse())
            .Message.ShouldContain("not a materialized view");

    [Fact]
    public void Parse_IndexOnUnknownRelation_Throws()
        => Should.Throw<DdlSyntaxException>(() =>
            new DdlParser("CREATE SCHEMA app; CREATE INDEX ix ON app.ghost (x);").Parse())
            .Message.ShouldContain("unknown table or materialized view");

    [Fact]
    public void Parse_DuplicateIndexOnView_Throws()
        => Should.Throw<DdlSyntaxException>(() => new DdlParser(
            "CREATE SCHEMA app; CREATE MATERIALIZED VIEW app.m AS SELECT x FROM app.t; " +
            "CREATE INDEX ix ON app.m (x); CREATE INDEX ix ON app.m (y);").Parse())
            .Message.ShouldContain("already declared");

    [Fact]
    public void Parse_DropMaterializedView_RecordsDroppedView()
        => new DdlParser("CREATE SCHEMA app; DROP MATERIALIZED VIEW app.daily;").Parse().Document.Schema
            .Schemas.ShouldHaveSingleItem().DroppedViews.ShouldHaveSingleItem().ShouldBe("daily");
}
