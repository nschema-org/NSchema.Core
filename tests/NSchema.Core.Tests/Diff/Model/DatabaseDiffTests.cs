using NSchema.Diff.Model;
using NSchema.Diff.Model.Columns;
using NSchema.Diff.Model.Enums;
using NSchema.Diff.Model.Schemas;
using NSchema.Diff.Model.Services;
using NSchema.Diff.Model.Tables;
using NSchema.Diff.Model.Views;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Domains;
using NSchema.Model.Enums;
using NSchema.Model.Schemas;
using NSchema.Model.Scripts;
using NSchema.Model.Tables;
using NSchema.Model.Views;

namespace NSchema.Tests.Diff.Model;

public sealed class DatabaseDiffTests
{
    private static DeploymentScript Deployment(string name) =>
        new(name, "SELECT 1;", null, DeploymentPhase.Pre);

    private static ChangeScript Change(string name) =>
        new(name, "UPDATE 1;", "app",
            ChangeTrigger.AddColumn, "users", "email");

    private static DatabaseDiff WithChangeScript(ChangeScript change)
    {
        var column = new ColumnDiff("email", ChangeKind.Add, new Column { Name = "email", Type = SqlType.Text }) { MigrationScript = change };
        var table = new TableDiff("app", "users", ChangeKind.Modify, Columns: [column]);
        return new DatabaseDiff([new SchemaDiff("app", Tables: [table])]);
    }

    [Fact]
    public void ChangeScripts_WalksTheNodesTheyRideOn()
    {
        var change = Change("backfill");

        WithChangeScript(change).ChangeScripts().ShouldBe(new[] { change });
    }

    [Fact]
    public void AllScripts_IsChangeScriptsThenDeploymentScripts()
    {
        var change = Change("backfill");
        var deploy = Deployment("seed");
        var diff = WithChangeScript(change) with { DeploymentScripts = [deploy] };

        diff.AllScripts().ShouldBe(new Script[] { change, deploy });
    }

    [Fact]
    public void IsEmpty_TrueForNoChangesAndNoDeploymentScripts()
        => new DatabaseDiff([]).IsEmpty.ShouldBeTrue();

    [Fact]
    public void IsEmpty_FalseWhenADeploymentScriptRuns()
    {
        var diff = new DatabaseDiff([]) with { DeploymentScripts = [Deployment("seed")] };

        diff.IsEmpty.ShouldBeFalse();
    }

    /// <summary>
    /// app.users, with billing.orders pointing an FK at it and billing.summary reading it.
    /// </summary>
    private static Database CurrentDatabase() => new Database
    {
        Schemas = [
        new Schema { Name = "app", Tables = [new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }] }] },
        new Schema { Name = "billing",
            Tables = [
                new Table { Name = "orders", Columns = [new Column { Name = "id", Type = SqlType.Int }],
                    ForeignKeys = [new ForeignKey { Name = "fk_orders_user", ColumnNames = ["id"], ReferencedSchema = "app", ReferencedTable = "users", ReferencedColumnNames = ["id"] }] },
            ],
            Views = [new View { Name = "summary", Body = "select * from app.users", DependsOn = [new ViewDependency("app", "users")] }] },
    ],
    };

    /// <summary>
    /// The difference a teardown produces before any scope is applied: everything goes.
    /// </summary>
    private static DatabaseDiff TeardownDiff() => new(
    [
        new SchemaDiff("app", ChangeKind.Remove, Tables: [new TableDiff("app", "users", ChangeKind.Remove)]),
        new SchemaDiff("billing", ChangeKind.Remove,
            Tables: [new TableDiff("billing", "orders", ChangeKind.Remove)],
            Views: [new ViewDiff("billing", "summary", ChangeKind.Remove)]),
    ]);

    [Fact]
    public void ScopedTo_Unscoped_LeavesTheDifferenceAlone()
    {
        // Act
        var result = TeardownDiff().ScopedTo(PlanningScope.All, CurrentDatabase());

        // Assert
        result.Value!.Schemas.Count.ShouldBe(2);
        result.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void ScopedTo_ScopedTeardown_SeversTheOutOfScopeForeignKey_WithoutDroppingItsTable()
    {
        // Act — tear down app only. billing.orders keeps its rows; only the constraint aimed at app.users goes.
        var result = TeardownDiff().ScopedTo(PlanningScope.To("app"), CurrentDatabase());

        // Assert
        var billing = result.Value!.Schemas.Single(s => s.Name == "billing");
        billing.Kind.ShouldBeNull(); // the run is not about billing; it just cannot avoid it
        var orders = billing.Tables.ShouldHaveSingleItem();
        orders.Kind.ShouldBe(ChangeKind.Modify);
        orders.ForeignKeys.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            f => f.Name.ShouldBe("fk_orders_user"),
            f => f.Kind.ShouldBe(ChangeKind.Remove));
    }

    [Fact]
    public void ScopedTo_ScopedTeardown_DropsTheOutOfScopeViewThatReadsIt()
    {
        // Act — a view's dependency is embedded in its body, so there is nothing to sever but the view.
        var result = TeardownDiff().ScopedTo(PlanningScope.To("app"), CurrentDatabase());

        // Assert
        var billing = result.Value!.Schemas.Single(s => s.Name == "billing");
        billing.Views.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            v => v.Name.ShouldBe("summary"),
            v => v.Kind.ShouldBe(ChangeKind.Remove));
    }

    [Fact]
    public void ScopedTo_ScopedTeardown_StillTearsDownTheScopedSchema()
    {
        // Act
        var result = TeardownDiff().ScopedTo(PlanningScope.To("app"), CurrentDatabase());

        // Assert
        var app = result.Value!.Schemas.Single(s => s.Name == "app");
        app.Kind.ShouldBe(ChangeKind.Remove);
        app.Tables.ShouldHaveSingleItem().Name.ShouldBe("users");
    }

    [Fact]
    public void ScopedTo_WhenItReachesOutOfScope_AssertsWhatTheModelStates_AndHedgesWhatItGuessed()
    {
        // Act — a plan that touches what it was not asked to touch must announce it, not do it quietly. And
        // the two edge kinds are not equally trustworthy: the foreign key names its table outright, while the
        // view was scanned out of SQL nobody parsed.
        var result = TeardownDiff().ScopedTo(PlanningScope.To("app"), CurrentDatabase());

        // Assert
        result.Diagnostics.Count.ShouldBe(2);

        var stated = result.Diagnostics.Single(d => d.Message.Contains("billing.orders.fk_orders_user"));
        stated.Source.ShouldBe("scope");
        stated.Message.ShouldNotContain("billing.summary");

        var inferred = result.Diagnostics.Single(d => d.Message.Contains("billing.summary"));
        inferred.Source.ShouldBe("scope");
        inferred.Message.ShouldContain("does not parse SQL");
    }

    [Fact]
    public void ScopedTo_ScopeThatDisturbsNothing_WidensNothing_AndIsQuiet()
    {
        // Arrange — tearing billing down costs app nothing: the dependencies point the other way.
        var result = TeardownDiff().ScopedTo(PlanningScope.To("billing"), CurrentDatabase());

        // Assert
        result.Value!.Schemas.ShouldHaveSingleItem().Name.ShouldBe("billing");
        result.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void ScopedTo_NarrowingOnly_DoesNotInventChangesForUnmanagedSchemas()
    {
        // Arrange — the ordinary case: an unrestricted plan derives its scope from what it manages, and
        // comparing whole states manufactures a removal for everything else. Narrowing must discard those.
        var diff = new DatabaseDiff(
        [
            new SchemaDiff("app", Tables: [new TableDiff("app", "users", ChangeKind.Add)]),
            new SchemaDiff("unmanaged", ChangeKind.Remove),
        ]);

        // Act
        var result = diff.ScopedTo(PlanningScope.To("app"), CurrentDatabase());

        // Assert
        result.Value!.Schemas.ShouldHaveSingleItem().Name.ShouldBe("app");
        result.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>
    /// The difference that removes app's status enum, and nothing else.
    /// </summary>
    private static DatabaseDiff EnumRemovalDiff() => new(
    [
        new SchemaDiff("app", Enums: [new EnumDiff("app", "status", ChangeKind.Remove)]),
    ]);

    /// <summary>
    /// app.status (an enum), with billing.orders.state declared against it — qualified or bare.
    /// </summary>
    private static Database DatabaseWithEnumTypedColumn(SqlType columnType) => new()
    {
        Schemas = [
        new Schema { Name = "app", Enums = [new EnumType { Name = "status", Values = ["new", "done"] }] },
        new Schema { Name = "billing",
            Tables = [new Table { Name = "orders", Columns = [new Column { Name = "state", Type = columnType }] }] },
    ],
    };

    [Fact]
    public void ScopedTo_RemovalAnOutOfScopeColumnDependsOn_Blocks_InsteadOfWidening()
    {
        // Arrange — closure severs definitions, never data: a column stands for its table's rows, so there is
        // no minimal sever. The plan is blocked, and still carried for review.
        var current = DatabaseWithEnumTypedColumn(SqlType.Custom("app", "status"));

        // Act
        var result = EnumRemovalDiff().ScopedTo(PlanningScope.To("app"), current);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Value!.Schemas.ShouldHaveSingleItem().Name.ShouldBe("app"); // nothing widened into billing
        result.Diagnostics.ShouldHaveSingleItem().ShouldBe(
            DiffDiagnostics.ColumnBlocksRemoval([new MemberAddress("billing", "orders", "state")]));
    }

    [Fact]
    public void ScopedTo_RemovalABareTypedColumnAppearsToDependOn_Hedges_InsteadOfBlocking()
    {
        // Arrange — the edge was bound by name alone, and a wrong guess must not block a plan that need not
        // be. The plan proceeds with a warning; if the guess was right, the database rejects it at apply.
        var current = DatabaseWithEnumTypedColumn(SqlType.Custom("status"));

        // Act
        var result = EnumRemovalDiff().ScopedTo(PlanningScope.To("app"), current);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Schemas.ShouldHaveSingleItem().Name.ShouldBe("app");
        result.Diagnostics.ShouldHaveSingleItem().ShouldBe(
            DiffDiagnostics.InferredColumnMayBlockRemoval([new MemberAddress("billing", "orders", "state")]));
    }

    [Fact]
    public void ScopedTo_RemovalAnOutOfScopeDomainIsBuiltOn_DropsTheDomain()
    {
        // Arrange — a domain is a definition, so it severs the way a view does.
        var current = new Database
        {
            Schemas = [
            new Schema { Name = "app", Enums = [new EnumType { Name = "status", Values = ["new", "done"] }] },
            new Schema { Name = "billing",
                Domains = [new DomainType { Name = "tracked_status", DataType = SqlType.Custom("app", "status") }] },
        ],
        };

        // Act
        var result = EnumRemovalDiff().ScopedTo(PlanningScope.To("app"), current);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var billing = result.Value!.Schemas.Single(s => s.Name == "billing");
        billing.Kind.ShouldBeNull();
        billing.Domains.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            d => d.Name.ShouldBe("tracked_status"),
            d => d.Kind.ShouldBe(ChangeKind.Remove));
        result.Diagnostics.ShouldHaveSingleItem().ShouldBe(
            DiffDiagnostics.SeveredOutOfScope([new ObjectAddress("billing", "tracked_status")]));
    }

    private static ObjectAddress Target(string schema, string name) => new(schema, name);

    [Fact]
    public void ScopedTo_ObjectTargeted_KeepsTheTargetsChanges_AndDropsItsSiblingsAndTheSchemasOwnFacets()
    {
        // Arrange — a schema-level facet (comment, grants, the schema's own removal) sits below the schema,
        // not below any object, so targeting an object never drags it in.
        var diff = new DatabaseDiff(
        [
            new SchemaDiff("app",
                Comment: new ValueChange<string>("old", "new"),
                Tables: [
                    new TableDiff("app", "users", ChangeKind.Modify),
                    new TableDiff("app", "orders", ChangeKind.Modify),
                ]),
        ]);

        // Act
        var result = diff.ScopedTo(PlanningScope.To([Target("app", "users")]), CurrentDatabase());

        // Assert
        var app = result.Value!.Schemas.ShouldHaveSingleItem();
        app.Comment.ShouldBeNull();
        app.Tables.ShouldHaveSingleItem().Name.ShouldBe("users");
    }

    [Fact]
    public void ScopedTo_ObjectTargeted_TheContainersCreationRidesAlong()
    {
        // Arrange — a targeted object cannot exist without its schema, so the container's Add is a
        // dependency of the target, not a schema-level facet to strip.
        var diff = new DatabaseDiff(
        [
            new SchemaDiff("app", ChangeKind.Add,
                Tables: [
                    new TableDiff("app", "users", ChangeKind.Add),
                    new TableDiff("app", "orders", ChangeKind.Add),
                ]),
        ]);

        // Act
        var result = diff.ScopedTo(PlanningScope.To([Target("app", "users")]), CurrentDatabase());

        // Assert
        var app = result.Value!.Schemas.ShouldHaveSingleItem();
        app.Kind.ShouldBe(ChangeKind.Add);
        app.Tables.ShouldHaveSingleItem().Name.ShouldBe("users");
    }

    [Fact]
    public void ScopedTo_ObjectTargetedTeardown_DoesNotRemoveTheContainer_AndStillSeversBeyondTheTarget()
    {
        // Act — target one table of the teardown: the schema itself stays, and the out-of-scope closure
        // works exactly as it does for a schema-granular scope.
        var result = TeardownDiff().ScopedTo(PlanningScope.To([Target("app", "users")]), CurrentDatabase());

        // Assert
        var app = result.Value!.Schemas.Single(s => s.Name == "app");
        app.Kind.ShouldBeNull(); // the container is not covered, so it is not removed
        app.Tables.ShouldHaveSingleItem().Name.ShouldBe("users");

        var billing = result.Value.Schemas.Single(s => s.Name == "billing");
        billing.Tables.ShouldHaveSingleItem().ForeignKeys.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Remove);
        billing.Views.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Remove);
        result.Diagnostics.Count.ShouldBe(2);
    }

    [Fact]
    public void ScopedTo_ATargetedDependent_IsInScopeThroughItsOwner_AndNotSevered()
    {
        // Act — billing.orders is targeted too, so its removal is part of the plan and its constraint goes
        // with the table; only the untargeted view is severed.
        var result = TeardownDiff().ScopedTo(
            PlanningScope.To([Target("app", "users"), Target("billing", "orders")]), CurrentDatabase());

        // Assert
        var billing = result.Value!.Schemas.Single(s => s.Name == "billing");
        billing.Tables.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Remove);
        billing.Views.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Remove);
        result.Diagnostics.ShouldHaveSingleItem().ShouldBe(
            DiffDiagnostics.InferredSeveredOutOfScope([new ObjectAddress("billing", "summary")]));
    }
}
