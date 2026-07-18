using NSchema.Model;
using NSchema.Model.Routines;
using NSchema.Model.Schemas;
using NSchema.Model.Scripts;
using NSchema.Model.Tables;
using NSchema.Model.Views;
using NSchema.Project.Model.Directives;
using NSchema.Project.Model.Services;

namespace NSchema.Tests.Project.Model;

public sealed class DatabaseTests
{
    private static Database Db(params Schema[] schemas) => new Database { Schemas = [.. schemas] };

    private static Schema Schema(string name, params Table[] tables) => new Schema { Name = new SqlIdentifier(name), Tables = [.. tables] };

    private static Table Table(string name) => new Table { Name = new SqlIdentifier(name) };

    private static View View(string name) => new View { Name = new SqlIdentifier(name), Body = new SqlText($"SELECT * FROM {name}_source") };

    private static Database Sample() => new Database
    {
        Schemas = [new Schema { Name = new SqlIdentifier("app") }, new Schema { Name = new SqlIdentifier("audit") }, new Schema { Name = new SqlIdentifier("legacy") }],
    };

    [Fact]
    public void ScopedTo_RestrictsSchemas()
    {
        var result = Sample().ScopedTo(PlanningScope.To(new SqlIdentifier("app")));

        result.Schemas.Select(s => s.Name).ShouldBe(["app"]);
    }

    [Fact]
    public void ScopedTo_IsCaseInsensitive()
    {
        var schema = new Database { Schemas = [new Schema { Name = new SqlIdentifier("App") }] };

        var result = schema.ScopedTo(PlanningScope.To(new SqlIdentifier("app")));

        result.Schemas.Select(s => s.Name).ShouldBe(["App"]);
    }

    [Fact]
    public void ScopedTo_NamesNotPresent_AreIgnored()
    {
        var result = Sample().ScopedTo(PlanningScope.To(new SqlIdentifier("app"), new SqlIdentifier("does-not-exist")));

        result.Schemas.Select(s => s.Name).ShouldBe(["app"]);
    }

    [Fact]
    public void ScopedTo_ObjectTargeted_KeepsTheContainerAndTheTargetAlone()
    {
        // A targeted object cannot live outside its schema, so the container stays in the tree — filtered
        // to the target, with every other schema gone.
        var database = Db(Schema("app", Table("users"), Table("orders")), Schema("audit", Table("log")));

        var result = database.ScopedTo(PlanningScope.To([new ObjectAddress(new SqlIdentifier("app"), new SqlIdentifier("users"))]));

        var app = result.Schemas.ShouldHaveSingleItem();
        app.Name.ShouldBe("app");
        app.Tables.ShouldHaveSingleItem().Name.ShouldBe("users");
    }

    [Fact]
    public void ScopedTo_RestrictsDirectivesToInScopeSchemas()
    {
        // Directives address current reality, so a schema rename keeps its object directives in scope through
        // either side; unrelated schemas' directives drop out.
        var sales = new SqlIdentifier("sales");
        var core = new SqlIdentifier("core");
        var project = new ProjectDefinition(
            new Database { Schemas = [new Schema { Name = core }, new Schema { Name = new SqlIdentifier("audit") }] },
            new ProjectDirectives(
                SchemaRenames: [new SchemaRenameDirective(sales, core)],
                ObjectRenames:
                [
                    new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, new ObjectAddress(sales, new SqlIdentifier("old"))), new SqlIdentifier("current")),
                    new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, new ObjectAddress(new SqlIdentifier("audit"), new SqlIdentifier("stale"))), new SqlIdentifier("fresh")),
                ]));

        var filtered = project.ScopedTo(PlanningScope.To(core)).Directives;

        filtered.SchemaRenames.ShouldHaveSingleItem(); // kept — its To side is in scope
        filtered.ObjectRenames.ShouldHaveSingleItem().From.Schema.ShouldBe(sales); // resolves through the rename
    }

    [Fact]
    public void ScopedTo_ObjectTargeted_DirectivesFollowTheContainmentRule()
    {
        // A change script rides the table it prepares; a deployment script is a schema-level facet, below
        // the schema and no object, so only a whole-schema scope carries it. Renames stay through either side.
        var app = new SqlIdentifier("app");
        var users = new ObjectAddress(app, new SqlIdentifier("users"));
        var directives = new ProjectDirectives(
            ObjectRenames:
            [
                new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, new ObjectAddress(app, new SqlIdentifier("customers"))), new SqlIdentifier("users")),
                new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, new ObjectAddress(app, new SqlIdentifier("stale"))), new SqlIdentifier("fresh")),
            ],
            MemberRenames: [new MemberRenameDirective(new MemberAddress(app, new SqlIdentifier("users"), new SqlIdentifier("mail")), new SqlIdentifier("email"))],
            ChangeScripts:
            [
                new ChangeScript(new SqlIdentifier("backfill"), new SqlText("UPDATE 1;"), app, ChangeTrigger.AddColumn, new SqlIdentifier("users"), new SqlIdentifier("email")),
                new ChangeScript(new SqlIdentifier("other"), new SqlText("UPDATE 2;"), app, ChangeTrigger.AddColumn, new SqlIdentifier("orders"), new SqlIdentifier("total")),
            ],
            DeploymentScripts: [new DeploymentScript(new SqlIdentifier("seed"), new SqlText("SELECT 1;"), app, DeploymentPhase.Pre)]);

        var filtered = directives.ScopedTo(PlanningScope.To([users]));

        filtered.ObjectRenames.ShouldHaveSingleItem().To.ShouldBe("users"); // kept through its target side
        filtered.MemberRenames.ShouldHaveSingleItem(); // its owner is the target
        filtered.ChangeScripts.ShouldHaveSingleItem().Name.ShouldBe("backfill");
        filtered.DeploymentScripts.ShouldBeEmpty();
    }

    [Fact]
    public void ScopedTo_AllScope_ReturnsEverything()
    {
        var schema = Sample();

        schema.ScopedTo(PlanningScope.All).ShouldBe(schema);
    }

    [Fact]
    public void ScopedTo_EmptyScope_NormalizesToAll()
    {
        var schema = Sample();

        schema.ScopedTo(PlanningScope.To()).ShouldBe(schema);
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
        var db1 = Db(new Schema { Name = new SqlIdentifier("public"), Routines = [new Routine { Name = new SqlIdentifier("f"), RoutineKind = RoutineKind.Function, Arguments = new SqlText(""), Definition = new SqlText("RETURNS int AS $$ SELECT 1 $$") }] });
        var db2 = Db(new Schema { Name = new SqlIdentifier("public"), Routines = [new Routine { Name = new SqlIdentifier("f"), RoutineKind = RoutineKind.Function, Arguments = new SqlText(""), Definition = new SqlText("RETURNS int AS $$ SELECT 2 $$") }] });

        DatabaseAggregator.Combine(db1, db2).Errors.ShouldHaveSingleItem().Message.ShouldContain("Duplicate routine 'f'");
    }

    [Fact]
    public void Combine_DuplicateProcedureInSameSchema_IsAnError()
    {
        var db1 = Db(new Schema { Name = new SqlIdentifier("public"), Routines = [new Routine { Name = new SqlIdentifier("p"), RoutineKind = RoutineKind.Procedure, Arguments = new SqlText(""), Definition = new SqlText("AS $$ SELECT 1 $$") }] });
        var db2 = Db(new Schema { Name = new SqlIdentifier("public"), Routines = [new Routine { Name = new SqlIdentifier("p"), RoutineKind = RoutineKind.Procedure, Arguments = new SqlText(""), Definition = new SqlText("AS $$ SELECT 2 $$") }] });

        DatabaseAggregator.Combine(db1, db2).Errors.ShouldHaveSingleItem().Message.ShouldContain("Duplicate routine 'p'");
    }

    [Fact]
    public void Combine_FunctionAndProcedureWithSameName_IsAnError()
    {
        // Functions and procedures share one name pool, as they do in the database's catalog.
        var db1 = Db(new Schema { Name = new SqlIdentifier("public"), Routines = [new Routine { Name = new SqlIdentifier("r"), RoutineKind = RoutineKind.Function, Arguments = new SqlText(""), Definition = new SqlText("RETURNS int AS $$ SELECT 1 $$") }] });
        var db2 = Db(new Schema { Name = new SqlIdentifier("public"), Routines = [new Routine { Name = new SqlIdentifier("r"), RoutineKind = RoutineKind.Procedure, Arguments = new SqlText(""), Definition = new SqlText("AS $$ SELECT 1 $$") }] });

        DatabaseAggregator.Combine(db1, db2).Errors.ShouldHaveSingleItem().Message.ShouldContain("share one name space");
    }

    // ── Partial schemas ───────────────────────────────────────────────────────

    // ── Comments ──────────────────────────────────────────────────────────────

    [Fact]
    public void Combine_Comment_FromOneOfMultipleProviders_IsPreserved()
    {
        // Arrange
        var db1 = Db(new Schema { Name = new SqlIdentifier("public"), Comment = "App schema" });
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
        var db1 = Db(new Schema { Name = new SqlIdentifier("public"), Comment = "App schema" });
        var db2 = Db(new Schema { Name = new SqlIdentifier("public"), Comment = "App schema" });

        // Act
        var result = DatabaseAggregator.Combine(db1, db2).Require();

        // Assert
        result.Schemas[0].Comment.ShouldBe("App schema");
    }

    [Fact]
    public void Combine_ConflictingComments_IsAnError()
    {
        // Arrange
        var db1 = Db(new Schema { Name = new SqlIdentifier("public"), Comment = "App schema" });
        var db2 = Db(new Schema { Name = new SqlIdentifier("public"), Comment = "Different comment" });

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
        var db1 = Db(new Schema { Name = new SqlIdentifier("public"), Grants = [new SchemaGrant(new SqlIdentifier("app_user"))] });
        var db2 = Db(new Schema { Name = new SqlIdentifier("public"), Grants = [new SchemaGrant(new SqlIdentifier("reporting"))] });

        // Act
        var result = DatabaseAggregator.Combine(db1, db2).Require();

        // Assert
        result.Schemas[0].Grants.Select(g => g.Role).ShouldBe(["app_user", "reporting"], ignoreOrder: true);
    }

    [Fact]
    public void Combine_DuplicateGrants_AreDeduplicated()
    {
        // Arrange
        var db1 = Db(new Schema { Name = new SqlIdentifier("public"), Grants = [new SchemaGrant(new SqlIdentifier("app_user"))] });
        var db2 = Db(new Schema { Name = new SqlIdentifier("public"), Grants = [new SchemaGrant(new SqlIdentifier("app_user"))] });

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
        var db1 = Db(new Schema { Name = new SqlIdentifier("public"), Views = [View("active_users")] });
        var db2 = Db(new Schema { Name = new SqlIdentifier("public"), Views = [View("user_summary")] });

        // Act
        var result = DatabaseAggregator.Combine(db1, db2).Require();

        // Assert
        result.Schemas[0].Views.Select(v => v.Name).ShouldBe(["active_users", "user_summary"]);
    }

    [Fact]
    public void Combine_DuplicateViewInSameSchema_IsAnError()
    {
        // Arrange
        var db1 = Db(new Schema { Name = new SqlIdentifier("public"), Views = [View("active_users")] });
        var db2 = Db(new Schema { Name = new SqlIdentifier("public"), Views = [View("active_users")] });

        // Act
        var result = DatabaseAggregator.Combine(db1, db2);

        // Assert
        var error = result.Errors.ShouldHaveSingleItem();
        error.Message.ShouldContain("active_users");
        error.Message.ShouldContain("public");
    }
}
