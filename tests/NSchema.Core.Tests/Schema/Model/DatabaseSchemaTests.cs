using NSchema.Project.Domain;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Routines;
using NSchema.Project.Domain.Models.Schemas;
using NSchema.Project.Domain.Models.Tables;
using NSchema.Project.Domain.Models.Views;

namespace NSchema.Tests.Schema.Model;

public sealed class DatabaseSchemaTests
{
    private static DatabaseSchema Db(params SchemaDefinition[] schemas) => new DatabaseSchema(schemas);

    private static SchemaDefinition Schema(string name, params Table[] tables) => new SchemaDefinition(new SqlIdentifier(name), Tables: tables);

    private static Table Table(string name) => new(new SqlIdentifier(name));

    private static View View(string name) => new(new SqlIdentifier(name), new SqlText($"SELECT * FROM {name}_source"));

    private static DatabaseSchema Sample() => new(
        [new SchemaDefinition(new SqlIdentifier("app")), new SchemaDefinition(new SqlIdentifier("audit")), new SchemaDefinition(new SqlIdentifier("legacy"))],
        [new SqlIdentifier("old"), new SqlIdentifier("scratch")]);

    [Fact]
    public void Filter_RestrictsBothSchemasAndDroppedSchemas()
    {
        var result = SchemaFilter.Apply(Sample(), SchemaScope.Of(new SqlIdentifier("app"), new SqlIdentifier("old")));

        result.Schemas.Select(s => s.Name).ShouldBe(["app"]);
        result.DroppedSchemas.ShouldBe(["old"]);
    }

    [Fact]
    public void Filter_IsCaseInsensitive()
    {
        var schema = new DatabaseSchema([new SchemaDefinition(new SqlIdentifier("App"))], [new SqlIdentifier("Old")]);

        var result = SchemaFilter.Apply(schema, SchemaScope.Of(new SqlIdentifier("app"), new SqlIdentifier("old")));

        result.Schemas.Select(s => s.Name).ShouldBe(["App"]);
        result.DroppedSchemas.ShouldBe(["Old"]);
    }

    [Fact]
    public void Filter_NamesNotPresent_AreIgnored()
    {
        var result = SchemaFilter.Apply(Sample(), SchemaScope.Of(new SqlIdentifier("app"), new SqlIdentifier("does-not-exist")));

        result.Schemas.Select(s => s.Name).ShouldBe(["app"]);
        result.DroppedSchemas.ShouldBeEmpty();
    }

    [Fact]
    public void Filter_AllScope_ReturnsEverything()
    {
        var schema = Sample();

        SchemaFilter.Apply(schema, SchemaScope.All).ShouldBe(schema);
    }

    [Fact]
    public void Filter_EmptyScope_NormalizesToAll()
    {
        var schema = Sample();

        SchemaFilter.Apply(schema, SchemaScope.Of()).ShouldBe(schema);
    }

    // ── Multiple providers, distinct schema names ─────────────────────────────

    [Fact]
    public void Combine_MultipleProviders_DistinctSchemaNames_ProducesAllSchemas()
    {
        // Arrange
        var db1 = Db(Schema("public", Table("users")));
        var db2 = Db(Schema("admin", Table("roles")));

        // Act
        var result = SchemaAggregator.Combine(db1, db2).Require();

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
        var result = SchemaAggregator.Combine(db1, db2).Require();

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
        var result = SchemaAggregator.Combine(db1, db2);

        // Assert
        var error = result.Errors.ShouldHaveSingleItem();
        error.Message.ShouldContain("users");
        error.Message.ShouldContain("public");
    }

    // ── Routines share one name space across documents ────────────────────────

    [Fact]
    public void Combine_DuplicateFunctionInSameSchema_IsAnError()
    {
        var db1 = Db(new SchemaDefinition(new SqlIdentifier("public"), Routines: [new Routine(new SqlIdentifier("f"), RoutineKind.Function, new SqlText(""), new SqlText("RETURNS int AS $$ SELECT 1 $$"))]));
        var db2 = Db(new SchemaDefinition(new SqlIdentifier("public"), Routines: [new Routine(new SqlIdentifier("f"), RoutineKind.Function, new SqlText(""), new SqlText("RETURNS int AS $$ SELECT 2 $$"))]));

        SchemaAggregator.Combine(db1, db2).Errors.ShouldHaveSingleItem().Message.ShouldContain("Duplicate routine 'f'");
    }

    [Fact]
    public void Combine_DuplicateProcedureInSameSchema_IsAnError()
    {
        var db1 = Db(new SchemaDefinition(new SqlIdentifier("public"), Routines: [new Routine(new SqlIdentifier("p"), RoutineKind.Procedure, new SqlText(""), new SqlText("AS $$ SELECT 1 $$"))]));
        var db2 = Db(new SchemaDefinition(new SqlIdentifier("public"), Routines: [new Routine(new SqlIdentifier("p"), RoutineKind.Procedure, new SqlText(""), new SqlText("AS $$ SELECT 2 $$"))]));

        SchemaAggregator.Combine(db1, db2).Errors.ShouldHaveSingleItem().Message.ShouldContain("Duplicate routine 'p'");
    }

    [Fact]
    public void Combine_FunctionAndProcedureWithSameName_IsAnError()
    {
        // Functions and procedures share one name pool, as they do in the database's catalog.
        var db1 = Db(new SchemaDefinition(new SqlIdentifier("public"), Routines: [new Routine(new SqlIdentifier("r"), RoutineKind.Function, new SqlText(""), new SqlText("RETURNS int AS $$ SELECT 1 $$"))]));
        var db2 = Db(new SchemaDefinition(new SqlIdentifier("public"), Routines: [new Routine(new SqlIdentifier("r"), RoutineKind.Procedure, new SqlText(""), new SqlText("AS $$ SELECT 1 $$"))]));

        SchemaAggregator.Combine(db1, db2).Errors.ShouldHaveSingleItem().Message.ShouldContain("share one name space");
    }

    // ── Partial schemas ───────────────────────────────────────────────────────

    [Fact]
    public void Combine_AnyProviderPartial_ResultIsPartial()
    {
        // Arrange
        var db1 = Db(new SchemaDefinition(new SqlIdentifier("public"), IsPartial: true));
        var db2 = Db(Schema("public", Table("posts")));

        // Act
        var result = SchemaAggregator.Combine(db1, db2).Require();

        // Assert
        result.Schemas[0].IsPartial.ShouldBeTrue();
    }

    [Fact]
    public void Combine_NoProviderPartial_ResultIsNotPartial()
    {
        // Arrange
        var db1 = Db(Schema("public", Table("users")));
        var db2 = Db(Schema("public", Table("posts")));

        // Act
        var result = SchemaAggregator.Combine(db1, db2).Require();

        // Assert
        result.Schemas[0].IsPartial.ShouldBeFalse();
    }

    [Fact]
    public void Combine_DroppedTables_AreCombinedAcrossProviders()
    {
        // Arrange
        var db1 = Db(new SchemaDefinition(new SqlIdentifier("public"), DroppedTables: [new SqlIdentifier("old_users")]));
        var db2 = Db(new SchemaDefinition(new SqlIdentifier("public"), DroppedTables: [new SqlIdentifier("legacy_data")]));

        // Act
        var result = SchemaAggregator.Combine(db1, db2).Require();

        // Assert
        result.Schemas[0].DroppedTables.ShouldBe(["old_users", "legacy_data"], ignoreOrder: true);
    }

    [Fact]
    public void Combine_DroppedSchemas_AreCombinedAcrossProviders()
    {
        // Arrange
        var db1 = new DatabaseSchema([], [new SqlIdentifier("old_schema")]);
        var db2 = new DatabaseSchema([], [new SqlIdentifier("legacy")]);

        // Act
        var result = SchemaAggregator.Combine(db1, db2).Require();

        // Assert
        result.DroppedSchemas.ShouldBe(["old_schema", "legacy"], ignoreOrder: true);
    }

    // ── Comments ──────────────────────────────────────────────────────────────

    [Fact]
    public void Combine_Comment_FromOneOfMultipleProviders_IsPreserved()
    {
        // Arrange
        var db1 = Db(new SchemaDefinition(new SqlIdentifier("public"), Comment: "App schema"));
        var db2 = Db(Schema("public", Table("posts")));

        // Act
        var result = SchemaAggregator.Combine(db1, db2).Require();

        // Assert
        result.Schemas[0].Comment.ShouldBe("App schema");
        result.Schemas[0].Tables.Select(t => t.Name).ShouldBe(["posts"]);
    }

    [Fact]
    public void Combine_SameCommentFromMultipleProviders_IsPreserved()
    {
        // Arrange
        var db1 = Db(new SchemaDefinition(new SqlIdentifier("public"), Comment: "App schema"));
        var db2 = Db(new SchemaDefinition(new SqlIdentifier("public"), Comment: "App schema"));

        // Act
        var result = SchemaAggregator.Combine(db1, db2).Require();

        // Assert
        result.Schemas[0].Comment.ShouldBe("App schema");
    }

    [Fact]
    public void Combine_ConflictingComments_IsAnError()
    {
        // Arrange
        var db1 = Db(new SchemaDefinition(new SqlIdentifier("public"), Comment: "App schema"));
        var db2 = Db(new SchemaDefinition(new SqlIdentifier("public"), Comment: "Different comment"));

        // Act
        var result = SchemaAggregator.Combine(db1, db2);

        // Assert
        var error = result.Errors.ShouldHaveSingleItem();
        error.Message.ShouldContain("public");
    }

    [Fact]
    public void Combine_ConflictingOldNames_IsAnError()
    {
        // Arrange — two providers disagree on what the schema was renamed from.
        var db1 = Db(new SchemaDefinition(new SqlIdentifier("public"), OldName: new SqlIdentifier("legacy")));
        var db2 = Db(new SchemaDefinition(new SqlIdentifier("public"), OldName: new SqlIdentifier("old_public")));

        // Act
        var result = SchemaAggregator.Combine(db1, db2);

        // Assert
        var error = result.Errors.ShouldHaveSingleItem();
        error.Message.ShouldContain("public");
    }

    [Fact]
    public void Combine_MatchingOldNames_AreCombined()
    {
        // Arrange — agreeing (or one-sided) rename sources combine without complaint.
        var db1 = Db(new SchemaDefinition(new SqlIdentifier("public"), OldName: new SqlIdentifier("legacy")));
        var db2 = Db(new SchemaDefinition(new SqlIdentifier("public")));

        // Act
        var result = SchemaAggregator.Combine(db1, db2).Require();

        // Assert
        result.Schemas.ShouldHaveSingleItem().OldName.ShouldBe("legacy");
    }

    // ── Grants ────────────────────────────────────────────────────────────────

    [Fact]
    public void Combine_Grants_AreCombinedAcrossProviders()
    {
        // Arrange
        var db1 = Db(new SchemaDefinition(new SqlIdentifier("public"), Grants: [new SchemaGrant(new SqlIdentifier("app_user"))]));
        var db2 = Db(new SchemaDefinition(new SqlIdentifier("public"), Grants: [new SchemaGrant(new SqlIdentifier("reporting"))]));

        // Act
        var result = SchemaAggregator.Combine(db1, db2).Require();

        // Assert
        result.Schemas[0].Grants.Select(g => g.Role).ShouldBe(["app_user", "reporting"], ignoreOrder: true);
    }

    [Fact]
    public void Combine_DuplicateGrants_AreDeduplicated()
    {
        // Arrange
        var db1 = Db(new SchemaDefinition(new SqlIdentifier("public"), Grants: [new SchemaGrant(new SqlIdentifier("app_user"))]));
        var db2 = Db(new SchemaDefinition(new SqlIdentifier("public"), Grants: [new SchemaGrant(new SqlIdentifier("app_user"))]));

        // Act
        var result = SchemaAggregator.Combine(db1, db2).Require();

        // Assert
        result.Schemas[0].Grants.Select(g => g.Role).ShouldBe(["app_user"]);
    }

    // ── Views ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Combine_MultipleProviders_SameSchemaName_MergesViews()
    {
        // Arrange
        var db1 = Db(new SchemaDefinition(new SqlIdentifier("public"), Views: [View("active_users")]));
        var db2 = Db(new SchemaDefinition(new SqlIdentifier("public"), Views: [View("user_summary")]));

        // Act
        var result = SchemaAggregator.Combine(db1, db2).Require();

        // Assert
        result.Schemas[0].Views.Select(v => v.Name).ShouldBe(["active_users", "user_summary"]);
    }

    [Fact]
    public void Combine_DuplicateViewInSameSchema_IsAnError()
    {
        // Arrange
        var db1 = Db(new SchemaDefinition(new SqlIdentifier("public"), Views: [View("active_users")]));
        var db2 = Db(new SchemaDefinition(new SqlIdentifier("public"), Views: [View("active_users")]));

        // Act
        var result = SchemaAggregator.Combine(db1, db2);

        // Assert
        var error = result.Errors.ShouldHaveSingleItem();
        error.Message.ShouldContain("active_users");
        error.Message.ShouldContain("public");
    }

    [Fact]
    public void Combine_DroppedViews_AreCombinedAcrossProviders()
    {
        // Arrange
        var db1 = Db(new SchemaDefinition(new SqlIdentifier("public"), DroppedViews: [new SqlIdentifier("old_view")]));
        var db2 = Db(new SchemaDefinition(new SqlIdentifier("public"), DroppedViews: [new SqlIdentifier("legacy_view")]));

        // Act
        var result = SchemaAggregator.Combine(db1, db2).Require();

        // Assert
        result.Schemas[0].DroppedViews.ShouldBe(["old_view", "legacy_view"], ignoreOrder: true);
    }
}
