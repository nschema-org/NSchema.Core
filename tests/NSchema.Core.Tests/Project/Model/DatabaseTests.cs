using NSchema.Project.Domain;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Routines;
using NSchema.Project.Domain.Models.Schemas;
using NSchema.Project.Domain.Models.Tables;
using NSchema.Project.Domain.Models.Views;

namespace NSchema.Tests.Project.Model;

public sealed class DatabaseTests
{
    private static Database Db(params Schema[] schemas) => new Database(schemas);

    private static Schema Schema(string name, params Table[] tables) => new Schema(new SqlIdentifier(name), Tables: tables);

    private static Table Table(string name) => new(new SqlIdentifier(name));

    private static View View(string name) => new(new SqlIdentifier(name), new SqlText($"SELECT * FROM {name}_source"));

    private static Database Sample() => new(
        [new Schema(new SqlIdentifier("app")), new Schema(new SqlIdentifier("audit")), new Schema(new SqlIdentifier("legacy"))]);

    [Fact]
    public void Filter_RestrictsSchemas()
    {
        var result = ScopeFilter.Apply(Sample(), SchemaScope.Of(new SqlIdentifier("app")));

        result.Schemas.Select(s => s.Name).ShouldBe(["app"]);
    }

    [Fact]
    public void Filter_IsCaseInsensitive()
    {
        var schema = new Database([new Schema(new SqlIdentifier("App"))]);

        var result = ScopeFilter.Apply(schema, SchemaScope.Of(new SqlIdentifier("app")));

        result.Schemas.Select(s => s.Name).ShouldBe(["App"]);
    }

    [Fact]
    public void Filter_NamesNotPresent_AreIgnored()
    {
        var result = ScopeFilter.Apply(Sample(), SchemaScope.Of(new SqlIdentifier("app"), new SqlIdentifier("does-not-exist")));

        result.Schemas.Select(s => s.Name).ShouldBe(["app"]);
    }

    [Fact]
    public void Filter_RestrictsDirectivesToInScopeSchemas()
    {
        // Directives address current reality, so a schema rename keeps its object directives in scope through
        // either side; unrelated schemas' directives drop out. Extension drops are global and pass through.
        var sales = new SqlIdentifier("sales");
        var core = new SqlIdentifier("core");
        var project = new ProjectDefinition(
            new Database([new Schema(core), new Schema(new SqlIdentifier("audit"))]),
            new ProjectDirectives(
                new NSchema.Project.Domain.Models.Schemas.SchemaDirectives(
                    Renames: [new SchemaRename(sales, core)],
                    Drops: [new SqlIdentifier("scratch")],
                    Partials: [core, new SqlIdentifier("audit")]),
                new NSchema.Project.Domain.Models.Tables.TableDirectives(
                    Drops:
                    [
                        new ObjectReference(sales, new SqlIdentifier("old")),
                        new ObjectReference(new SqlIdentifier("audit"), new SqlIdentifier("stale")),
                    ]),
                Extensions: new NSchema.Project.Domain.Models.Extensions.ExtensionDirectives(Drops: [new SqlIdentifier("stale_ext")])));

        var filtered = ScopeFilter.Apply(project, SchemaScope.Of(core)).Directives;

        filtered.Schemas.Renames.ShouldHaveSingleItem(); // kept — its To side is in scope
        filtered.Schemas.Drops.ShouldBeEmpty();          // scratch is out of scope
        filtered.Schemas.Partials.ShouldBe([core]);
        filtered.Tables.Drops.ShouldHaveSingleItem().Schema.ShouldBe(sales); // resolves through the rename
        filtered.Extensions.Drops.ShouldHaveSingleItem();
    }

    [Fact]
    public void Filter_AllScope_ReturnsEverything()
    {
        var schema = Sample();

        ScopeFilter.Apply(schema, SchemaScope.All).ShouldBe(schema);
    }

    [Fact]
    public void Filter_EmptyScope_NormalizesToAll()
    {
        var schema = Sample();

        ScopeFilter.Apply(schema, SchemaScope.Of()).ShouldBe(schema);
    }

    // ── Multiple providers, distinct schema names ─────────────────────────────

    [Fact]
    public void Combine_MultipleProviders_DistinctSchemaNames_ProducesAllSchemas()
    {
        // Arrange
        var db1 = Db(Schema("public", Table("users")));
        var db2 = Db(Schema("admin", Table("roles")));

        // Act
        var result = DatabaseAggregator.Combine(db1, db2).Require();

        // Assert
        result.Schemas.Count.ShouldBe(2);
        result.Schemas.Select(s => s.Name).ShouldBe(["public", "admin"]);
    }

    // ── Multiple providers, same schema name ──────────────────────────────────

    [Fact]
    public void Combine_MultipleProviders_SameSchemaName_MergesTables()
    {
        // Arrange
        var db1 = Db(Schema("public", Table("users")));
        var db2 = Db(Schema("public", Table("posts")));

        // Act
        var result = DatabaseAggregator.Combine(db1, db2).Require();

        // Assert
        result.Schemas.Count.ShouldBe(1);
        result.Schemas[0].Tables.Select(t => t.Name).ShouldBe(["users", "posts"]);
    }

    [Fact]
    public void Combine_DuplicateTableInSameSchema_IsAnError()
    {
        // Arrange
        var db1 = Db(Schema("public", Table("users")));
        var db2 = Db(Schema("public", Table("users")));

        // Act
        var result = DatabaseAggregator.Combine(db1, db2);

        // Assert
        var error = result.Errors.ShouldHaveSingleItem();
        error.Message.ShouldContain("users");
        error.Message.ShouldContain("public");
    }

    // ── Routines share one name space across documents ────────────────────────

    [Fact]
    public void Combine_DuplicateFunctionInSameSchema_IsAnError()
    {
        var db1 = Db(new Schema(new SqlIdentifier("public"), Routines: [new Routine(new SqlIdentifier("f"), RoutineKind.Function, new SqlText(""), new SqlText("RETURNS int AS $$ SELECT 1 $$"))]));
        var db2 = Db(new Schema(new SqlIdentifier("public"), Routines: [new Routine(new SqlIdentifier("f"), RoutineKind.Function, new SqlText(""), new SqlText("RETURNS int AS $$ SELECT 2 $$"))]));

        DatabaseAggregator.Combine(db1, db2).Errors.ShouldHaveSingleItem().Message.ShouldContain("Duplicate routine 'f'");
    }

    [Fact]
    public void Combine_DuplicateProcedureInSameSchema_IsAnError()
    {
        var db1 = Db(new Schema(new SqlIdentifier("public"), Routines: [new Routine(new SqlIdentifier("p"), RoutineKind.Procedure, new SqlText(""), new SqlText("AS $$ SELECT 1 $$"))]));
        var db2 = Db(new Schema(new SqlIdentifier("public"), Routines: [new Routine(new SqlIdentifier("p"), RoutineKind.Procedure, new SqlText(""), new SqlText("AS $$ SELECT 2 $$"))]));

        DatabaseAggregator.Combine(db1, db2).Errors.ShouldHaveSingleItem().Message.ShouldContain("Duplicate routine 'p'");
    }

    [Fact]
    public void Combine_FunctionAndProcedureWithSameName_IsAnError()
    {
        // Functions and procedures share one name pool, as they do in the database's catalog.
        var db1 = Db(new Schema(new SqlIdentifier("public"), Routines: [new Routine(new SqlIdentifier("r"), RoutineKind.Function, new SqlText(""), new SqlText("RETURNS int AS $$ SELECT 1 $$"))]));
        var db2 = Db(new Schema(new SqlIdentifier("public"), Routines: [new Routine(new SqlIdentifier("r"), RoutineKind.Procedure, new SqlText(""), new SqlText("AS $$ SELECT 1 $$"))]));

        DatabaseAggregator.Combine(db1, db2).Errors.ShouldHaveSingleItem().Message.ShouldContain("share one name space");
    }

    // ── Partial schemas ───────────────────────────────────────────────────────

    // ── Comments ──────────────────────────────────────────────────────────────

    [Fact]
    public void Combine_Comment_FromOneOfMultipleProviders_IsPreserved()
    {
        // Arrange
        var db1 = Db(new Schema(new SqlIdentifier("public"), Comment: "App schema"));
        var db2 = Db(Schema("public", Table("posts")));

        // Act
        var result = DatabaseAggregator.Combine(db1, db2).Require();

        // Assert
        result.Schemas[0].Comment.ShouldBe("App schema");
        result.Schemas[0].Tables.Select(t => t.Name).ShouldBe(["posts"]);
    }

    [Fact]
    public void Combine_SameCommentFromMultipleProviders_IsPreserved()
    {
        // Arrange
        var db1 = Db(new Schema(new SqlIdentifier("public"), Comment: "App schema"));
        var db2 = Db(new Schema(new SqlIdentifier("public"), Comment: "App schema"));

        // Act
        var result = DatabaseAggregator.Combine(db1, db2).Require();

        // Assert
        result.Schemas[0].Comment.ShouldBe("App schema");
    }

    [Fact]
    public void Combine_ConflictingComments_IsAnError()
    {
        // Arrange
        var db1 = Db(new Schema(new SqlIdentifier("public"), Comment: "App schema"));
        var db2 = Db(new Schema(new SqlIdentifier("public"), Comment: "Different comment"));

        // Act
        var result = DatabaseAggregator.Combine(db1, db2);

        // Assert
        var error = result.Errors.ShouldHaveSingleItem();
        error.Message.ShouldContain("public");
    }

    // ── Grants ────────────────────────────────────────────────────────────────

    [Fact]
    public void Combine_Grants_AreCombinedAcrossProviders()
    {
        // Arrange
        var db1 = Db(new Schema(new SqlIdentifier("public"), Grants: [new SchemaGrant(new SqlIdentifier("app_user"))]));
        var db2 = Db(new Schema(new SqlIdentifier("public"), Grants: [new SchemaGrant(new SqlIdentifier("reporting"))]));

        // Act
        var result = DatabaseAggregator.Combine(db1, db2).Require();

        // Assert
        result.Schemas[0].Grants.Select(g => g.Role).ShouldBe(["app_user", "reporting"], ignoreOrder: true);
    }

    [Fact]
    public void Combine_DuplicateGrants_AreDeduplicated()
    {
        // Arrange
        var db1 = Db(new Schema(new SqlIdentifier("public"), Grants: [new SchemaGrant(new SqlIdentifier("app_user"))]));
        var db2 = Db(new Schema(new SqlIdentifier("public"), Grants: [new SchemaGrant(new SqlIdentifier("app_user"))]));

        // Act
        var result = DatabaseAggregator.Combine(db1, db2).Require();

        // Assert
        result.Schemas[0].Grants.Select(g => g.Role).ShouldBe(["app_user"]);
    }

    // ── Views ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Combine_MultipleProviders_SameSchemaName_MergesViews()
    {
        // Arrange
        var db1 = Db(new Schema(new SqlIdentifier("public"), Views: [View("active_users")]));
        var db2 = Db(new Schema(new SqlIdentifier("public"), Views: [View("user_summary")]));

        // Act
        var result = DatabaseAggregator.Combine(db1, db2).Require();

        // Assert
        result.Schemas[0].Views.Select(v => v.Name).ShouldBe(["active_users", "user_summary"]);
    }

    [Fact]
    public void Combine_DuplicateViewInSameSchema_IsAnError()
    {
        // Arrange
        var db1 = Db(new Schema(new SqlIdentifier("public"), Views: [View("active_users")]));
        var db2 = Db(new Schema(new SqlIdentifier("public"), Views: [View("active_users")]));

        // Act
        var result = DatabaseAggregator.Combine(db1, db2);

        // Assert
        var error = result.Errors.ShouldHaveSingleItem();
        error.Message.ShouldContain("active_users");
        error.Message.ShouldContain("public");
    }
}
