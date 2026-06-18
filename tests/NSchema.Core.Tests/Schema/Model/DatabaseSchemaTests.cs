using NSchema.Schema.Model;
using NSchema.Schema.Model.Routines;
using NSchema.Schema.Model.Schemas;
using NSchema.Schema.Model.Tables;
using NSchema.Schema.Model.Views;

namespace NSchema.Tests.Schema.Model;

public sealed class DatabaseSchemaTests
{
    private static DatabaseSchema Db(params SchemaDefinition[] schemas) => new DatabaseSchema(schemas);

    private static SchemaDefinition Schema(string name, params Table[] tables) => new SchemaDefinition(name, Tables: tables);

    private static Table Table(string name) => new(name);

    private static View View(string name) => new(name, $"SELECT * FROM {name}_source");

    private static DatabaseSchema Sample() => new(
        [new SchemaDefinition("app"), new SchemaDefinition("audit"), new SchemaDefinition("legacy")],
        ["old", "scratch"]);

    [Fact]
    public void Filter_RestrictsBothSchemasAndDroppedSchemas()
    {
        var result = Sample().Filter(["app", "old"]);

        result.Schemas.Select(s => s.Name).ShouldBe(["app"]);
        result.DroppedSchemas.ShouldBe(["old"]);
    }

    [Fact]
    public void Filter_IsCaseInsensitive()
    {
        var schema = new DatabaseSchema([new SchemaDefinition("App")], ["Old"]);

        var result = schema.Filter(["app", "old"]);

        result.Schemas.Select(s => s.Name).ShouldBe(["App"]);
        result.DroppedSchemas.ShouldBe(["Old"]);
    }

    [Fact]
    public void Filter_NamesNotPresent_AreIgnored()
    {
        var result = Sample().Filter(["app", "does-not-exist"]);

        result.Schemas.Select(s => s.Name).ShouldBe(["app"]);
        result.DroppedSchemas.ShouldBeEmpty();
    }

    [Fact]
    public void Filter_NullScope_ReturnsEverything()
    {
        var schema = Sample();

        schema.Filter(null).ShouldBe(schema);
    }

    [Fact]
    public void Filter_EmptyScope_ReturnsEverything()
    {
        var schema = Sample();

        schema.Filter([]).ShouldBe(schema);
    }

    // ── Multiple providers, distinct schema names ─────────────────────────────

    [Fact]
    public void Combine_MultipleProviders_DistinctSchemaNames_ProducesAllSchemas()
    {
        // Arrange
        var db1 = Db(Schema("public", Table("users")));
        var db2 = Db(Schema("admin", Table("roles")));

        // Act
        var result = db1.Combine(db2);

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
        var result = db1.Combine(db2);

        // Assert
        result.Schemas.Count.ShouldBe(1);
        result.Schemas[0].Tables.Select(t => t.Name).ShouldBe(["users", "posts"]);
    }

    [Fact]
    public void Combine_DuplicateTableInSameSchema_Throws()
    {
        // Arrange
        var db1 = Db(Schema("public", Table("users")));
        var db2 = Db(Schema("public", Table("users")));

        // Act
        var act = () => db1.Combine(db2);

        // Assert
        var ex = act.ShouldThrow<InvalidOperationException>();
        ex.Message.ShouldContain("users");
        ex.Message.ShouldContain("public");
    }

    // ── Routines share one name space across documents ────────────────────────

    [Fact]
    public void Combine_DuplicateFunctionInSameSchema_Throws()
    {
        var db1 = Db(new SchemaDefinition("public", Routines: [new Routine("f", RoutineKind.Function, "", "RETURNS int AS $$ SELECT 1 $$")]));
        var db2 = Db(new SchemaDefinition("public", Routines: [new Routine("f", RoutineKind.Function, "", "RETURNS int AS $$ SELECT 2 $$")]));

        var act = () => db1.Combine(db2);

        act.ShouldThrow<InvalidOperationException>().Message.ShouldContain("Duplicate routine 'f'");
    }

    [Fact]
    public void Combine_DuplicateProcedureInSameSchema_Throws()
    {
        var db1 = Db(new SchemaDefinition("public", Routines: [new Routine("p", RoutineKind.Procedure, "", "AS $$ SELECT 1 $$")]));
        var db2 = Db(new SchemaDefinition("public", Routines: [new Routine("p", RoutineKind.Procedure, "", "AS $$ SELECT 2 $$")]));

        var act = () => db1.Combine(db2);

        act.ShouldThrow<InvalidOperationException>().Message.ShouldContain("Duplicate routine 'p'");
    }

    [Fact]
    public void Combine_FunctionAndProcedureWithSameName_Throws()
    {
        // Functions and procedures share one name pool, as they do in the database's catalog.
        var db1 = Db(new SchemaDefinition("public", Routines: [new Routine("r", RoutineKind.Function, "", "RETURNS int AS $$ SELECT 1 $$")]));
        var db2 = Db(new SchemaDefinition("public", Routines: [new Routine("r", RoutineKind.Procedure, "", "AS $$ SELECT 1 $$")]));

        var act = () => db1.Combine(db2);

        act.ShouldThrow<InvalidOperationException>().Message.ShouldContain("share one name space");
    }

    // ── Partial schemas ───────────────────────────────────────────────────────

    [Fact]
    public void Combine_AnyProviderPartial_ResultIsPartial()
    {
        // Arrange
        var db1 = Db(new SchemaDefinition("public", IsPartial: true));
        var db2 = Db(Schema("public", Table("posts")));

        // Act
        var result = db1.Combine(db2);

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
        var result = db1.Combine(db2);

        // Assert
        result.Schemas[0].IsPartial.ShouldBeFalse();
    }

    [Fact]
    public void Combine_DroppedTables_AreCombinedAcrossProviders()
    {
        // Arrange
        var db1 = Db(new SchemaDefinition("public", DroppedTables: ["old_users"]));
        var db2 = Db(new SchemaDefinition("public", DroppedTables: ["legacy_data"]));

        // Act
        var result = db1.Combine(db2);

        // Assert
        result.Schemas[0].DroppedTables.ShouldBe(["old_users", "legacy_data"], ignoreOrder: true);
    }

    [Fact]
    public void Combine_DroppedSchemas_AreCombinedAcrossProviders()
    {
        // Arrange
        var db1 = new DatabaseSchema([], ["old_schema"]);
        var db2 = new DatabaseSchema([], ["legacy"]);

        // Act
        var result = db1.Combine(db2);

        // Assert
        result.DroppedSchemas.ShouldBe(["old_schema", "legacy"], ignoreOrder: true);
    }

    // ── Comments ──────────────────────────────────────────────────────────────

    [Fact]
    public void Combine_Comment_FromOneOfMultipleProviders_IsPreserved()
    {
        // Arrange
        var db1 = Db(new SchemaDefinition("public", Comment: "App schema"));
        var db2 = Db(Schema("public", Table("posts")));

        // Act
        var result = db1.Combine(db2);

        // Assert
        result.Schemas[0].Comment.ShouldBe("App schema");
        result.Schemas[0].Tables.Select(t => t.Name).ShouldBe(["posts"]);
    }

    [Fact]
    public void Combine_SameCommentFromMultipleProviders_IsPreserved()
    {
        // Arrange
        var db1 = Db(new SchemaDefinition("public", Comment: "App schema"));
        var db2 = Db(new SchemaDefinition("public", Comment: "App schema"));

        // Act
        var result = db1.Combine(db2);

        // Assert
        result.Schemas[0].Comment.ShouldBe("App schema");
    }

    [Fact]
    public void Combine_ConflictingComments_Throws()
    {
        // Arrange
        var db1 = Db(new SchemaDefinition("public", Comment: "App schema"));
        var db2 = Db(new SchemaDefinition("public", Comment: "Different comment"));

        // Act
        var act = () => db1.Combine(db2);

        // Assert
        var ex = act.ShouldThrow<InvalidOperationException>();
        ex.Message.ShouldContain("public");
    }

    [Fact]
    public void Combine_ConflictingOldNames_Throws()
    {
        // Arrange — two providers disagree on what the schema was renamed from.
        var db1 = Db(new SchemaDefinition("public", OldName: "legacy"));
        var db2 = Db(new SchemaDefinition("public", OldName: "old_public"));

        // Act
        var act = () => db1.Combine(db2);

        // Assert
        var ex = act.ShouldThrow<InvalidOperationException>();
        ex.Message.ShouldContain("public");
    }

    [Fact]
    public void Combine_MatchingOldNames_AreCombined()
    {
        // Arrange — agreeing (or one-sided) rename sources combine without complaint.
        var db1 = Db(new SchemaDefinition("public", OldName: "legacy"));
        var db2 = Db(new SchemaDefinition("public"));

        // Act
        var result = db1.Combine(db2);

        // Assert
        result.Schemas.ShouldHaveSingleItem().OldName.ShouldBe("legacy");
    }

    // ── Grants ────────────────────────────────────────────────────────────────

    [Fact]
    public void Combine_Grants_AreCombinedAcrossProviders()
    {
        // Arrange
        var db1 = Db(new SchemaDefinition("public", Grants: [new SchemaGrant("app_user")]));
        var db2 = Db(new SchemaDefinition("public", Grants: [new SchemaGrant("reporting")]));

        // Act
        var result = db1.Combine(db2);

        // Assert
        result.Schemas[0].Grants.Select(g => g.Role).ShouldBe(["app_user", "reporting"], ignoreOrder: true);
    }

    [Fact]
    public void Combine_DuplicateGrants_AreDeduplicated()
    {
        // Arrange
        var db1 = Db(new SchemaDefinition("public", Grants: [new SchemaGrant("app_user")]));
        var db2 = Db(new SchemaDefinition("public", Grants: [new SchemaGrant("app_user")]));

        // Act
        var result = db1.Combine(db2);

        // Assert
        result.Schemas[0].Grants.Select(g => g.Role).ShouldBe(["app_user"]);
    }

    // ── Views ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Combine_MultipleProviders_SameSchemaName_MergesViews()
    {
        // Arrange
        var db1 = Db(new SchemaDefinition("public", Views: [View("active_users")]));
        var db2 = Db(new SchemaDefinition("public", Views: [View("user_summary")]));

        // Act
        var result = db1.Combine(db2);

        // Assert
        result.Schemas[0].Views.Select(v => v.Name).ShouldBe(["active_users", "user_summary"]);
    }

    [Fact]
    public void Combine_DuplicateViewInSameSchema_Throws()
    {
        // Arrange
        var db1 = Db(new SchemaDefinition("public", Views: [View("active_users")]));
        var db2 = Db(new SchemaDefinition("public", Views: [View("active_users")]));

        // Act
        var act = () => db1.Combine(db2);

        // Assert
        var ex = act.ShouldThrow<InvalidOperationException>();
        ex.Message.ShouldContain("active_users");
        ex.Message.ShouldContain("public");
    }

    [Fact]
    public void Combine_DroppedViews_AreCombinedAcrossProviders()
    {
        // Arrange
        var db1 = Db(new SchemaDefinition("public", DroppedViews: ["old_view"]));
        var db2 = Db(new SchemaDefinition("public", DroppedViews: ["legacy_view"]));

        // Act
        var result = db1.Combine(db2);

        // Assert
        result.Schemas[0].DroppedViews.ShouldBe(["old_view", "legacy_view"], ignoreOrder: true);
    }
}
