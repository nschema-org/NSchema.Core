using NSchema.Model;
using NSchema.Model.Schemas;
using NSchema.Model.Scripts;
using NSchema.Model.Tables;
using NSchema.Project.Model.Directives;

namespace NSchema.Tests.Project.Model;

public sealed class DatabaseTests
{
    private static Database Db(params Schema[] schemas) => new Database { Schemas = [.. schemas] };

    private static Schema Schema(string name, params Table[] tables) => new Schema { Name = new SqlIdentifier(name), Tables = [.. tables] };

    private static Table Table(string name) => new Table { Name = new SqlIdentifier(name) };

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
}
