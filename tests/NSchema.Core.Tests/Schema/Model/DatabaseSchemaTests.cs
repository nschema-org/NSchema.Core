using NSchema.Schema.Model;

namespace NSchema.Tests.Schema.Model;

public sealed class DatabaseSchemaTests
{
    private static DatabaseSchema Db(params SchemaDefinition[] schemas) => DatabaseSchema.Create(schemas);

    private static SchemaDefinition Schema(string name, params Table[] tables) => SchemaDefinition.Create(name, tables: tables);

    private static Table Table(string name) => NSchema.Schema.Model.Table.Create(name);

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

    // ── Partial schemas ───────────────────────────────────────────────────────

    [Fact]
    public void Combine_AnyProviderPartial_ResultIsPartial()
    {
        // Arrange
        var db1 = Db(SchemaDefinition.Create("public", isPartial: true));
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
        var db1 = Db(SchemaDefinition.Create("public", droppedTables: ["old_users"]));
        var db2 = Db(SchemaDefinition.Create("public", droppedTables: ["legacy_data"]));

        // Act
        var result = db1.Combine(db2);

        // Assert
        result.Schemas[0].DroppedTables.ShouldBe(["old_users", "legacy_data"], ignoreOrder: true);
    }

    [Fact]
    public void Combine_DroppedSchemas_AreCombinedAcrossProviders()
    {
        // Arrange
        var db1 = DatabaseSchema.Create([], ["old_schema"]);
        var db2 = DatabaseSchema.Create([], ["legacy"]);

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
        var db1 = Db(SchemaDefinition.Create("public", comment: "App schema"));
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
        var db1 = Db(SchemaDefinition.Create("public", comment: "App schema"));
        var db2 = Db(SchemaDefinition.Create("public", comment: "App schema"));

        // Act
        var result = db1.Combine(db2);

        // Assert
        result.Schemas[0].Comment.ShouldBe("App schema");
    }

    [Fact]
    public void Combine_ConflictingComments_Throws()
    {
        // Arrange
        var db1 = Db(SchemaDefinition.Create("public", comment: "App schema"));
        var db2 = Db(SchemaDefinition.Create("public", comment: "Different comment"));

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
        var db1 = Db(SchemaDefinition.Create("public", oldName: "legacy"));
        var db2 = Db(SchemaDefinition.Create("public", oldName: "old_public"));

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
        var db1 = Db(SchemaDefinition.Create("public", oldName: "legacy"));
        var db2 = Db(SchemaDefinition.Create("public"));

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
        var db1 = Db(SchemaDefinition.Create("public", grants: [new SchemaGrant("app_user")]));
        var db2 = Db(SchemaDefinition.Create("public", grants: [new SchemaGrant("reporting")]));

        // Act
        var result = db1.Combine(db2);

        // Assert
        result.Schemas[0].Grants.Select(g => g.Role).ShouldBe(["app_user", "reporting"], ignoreOrder: true);
    }

    [Fact]
    public void Combine_DuplicateGrants_AreDeduplicated()
    {
        // Arrange
        var db1 = Db(SchemaDefinition.Create("public", grants: [new SchemaGrant("app_user")]));
        var db2 = Db(SchemaDefinition.Create("public", grants: [new SchemaGrant("app_user")]));

        // Act
        var result = db1.Combine(db2);

        // Assert
        result.Schemas[0].Grants.Select(g => g.Role).ShouldBe(["app_user"]);
    }
}
