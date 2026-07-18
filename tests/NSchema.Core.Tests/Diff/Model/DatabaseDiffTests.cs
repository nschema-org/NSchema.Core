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
        new(new SqlIdentifier(name), new SqlText("SELECT 1;"), null, DeploymentPhase.Pre);

    private static ChangeScript Change(string name) =>
        new(new SqlIdentifier(name), new SqlText("UPDATE 1;"), new SqlIdentifier("app"),
            ChangeTrigger.AddColumn, new SqlIdentifier("users"), new SqlIdentifier("email"));

    private static DatabaseDiff WithChangeScript(ChangeScript change)
    {
        var column = new ColumnDiff(new SqlIdentifier("email"), ChangeKind.Add, new Column { Name = new SqlIdentifier("email"), Type = SqlType.Text }) { MigrationScript = change };
        var table = new TableDiff(new SqlIdentifier("app"), new SqlIdentifier("users"), ChangeKind.Modify, Columns: [column]);
        return new DatabaseDiff([new SchemaDiff(new SqlIdentifier("app"), Tables: [table])]);
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
        new Schema { Name = new SqlIdentifier("app"), Tables = [new Table { Name = new SqlIdentifier("users"), Columns = [new Column { Name = new SqlIdentifier("id"), Type = SqlType.Int }] }] },
        new Schema { Name = new SqlIdentifier("billing"),
            Tables = [
                new Table { Name = new SqlIdentifier("orders"), Columns = [new Column { Name = new SqlIdentifier("id"), Type = SqlType.Int }],
                    ForeignKeys = [new ForeignKey { Name = new SqlIdentifier("fk_orders_user"), ColumnNames = [new SqlIdentifier("id")], ReferencedSchema = new SqlIdentifier("app"), ReferencedTable = new SqlIdentifier("users"), ReferencedColumnNames = [new SqlIdentifier("id")] }] },
            ],
            Views = [new View { Name = new SqlIdentifier("summary"), Body = new SqlText("select * from app.users"), DependsOn = [new ViewDependency(new SqlIdentifier("app"), new SqlIdentifier("users"))] }] },
    ],
    };

    /// <summary>
    /// The difference a teardown produces before any scope is applied: everything goes.
    /// </summary>
    private static DatabaseDiff TeardownDiff() => new(
    [
        new SchemaDiff(new SqlIdentifier("app"), ChangeKind.Remove, Tables: [new TableDiff(new SqlIdentifier("app"), new SqlIdentifier("users"), ChangeKind.Remove)]),
        new SchemaDiff(new SqlIdentifier("billing"), ChangeKind.Remove,
            Tables: [new TableDiff(new SqlIdentifier("billing"), new SqlIdentifier("orders"), ChangeKind.Remove)],
            Views: [new ViewDiff(new SqlIdentifier("billing"), new SqlIdentifier("summary"), ChangeKind.Remove)]),
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
        var result = TeardownDiff().ScopedTo(PlanningScope.To(new SqlIdentifier("app")), CurrentDatabase());

        // Assert
        var billing = result.Value!.Schemas.Single(s => s.Name == new SqlIdentifier("billing"));
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
        var result = TeardownDiff().ScopedTo(PlanningScope.To(new SqlIdentifier("app")), CurrentDatabase());

        // Assert
        var billing = result.Value!.Schemas.Single(s => s.Name == new SqlIdentifier("billing"));
        billing.Views.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            v => v.Name.ShouldBe("summary"),
            v => v.Kind.ShouldBe(ChangeKind.Remove));
    }

    [Fact]
    public void ScopedTo_ScopedTeardown_StillTearsDownTheScopedSchema()
    {
        // Act
        var result = TeardownDiff().ScopedTo(PlanningScope.To(new SqlIdentifier("app")), CurrentDatabase());

        // Assert
        var app = result.Value!.Schemas.Single(s => s.Name == new SqlIdentifier("app"));
        app.Kind.ShouldBe(ChangeKind.Remove);
        app.Tables.ShouldHaveSingleItem().Name.ShouldBe("users");
    }

    [Fact]
    public void ScopedTo_WhenItReachesOutOfScope_AssertsWhatTheModelStates_AndHedgesWhatItGuessed()
    {
        // Act — a plan that touches what it was not asked to touch must announce it, not do it quietly. And
        // the two edge kinds are not equally trustworthy: the foreign key names its table outright, while the
        // view was scanned out of SQL nobody parsed.
        var result = TeardownDiff().ScopedTo(PlanningScope.To(new SqlIdentifier("app")), CurrentDatabase());

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
        var result = TeardownDiff().ScopedTo(PlanningScope.To(new SqlIdentifier("billing")), CurrentDatabase());

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
            new SchemaDiff(new SqlIdentifier("app"), Tables: [new TableDiff(new SqlIdentifier("app"), new SqlIdentifier("users"), ChangeKind.Add)]),
            new SchemaDiff(new SqlIdentifier("unmanaged"), ChangeKind.Remove),
        ]);

        // Act
        var result = diff.ScopedTo(PlanningScope.To(new SqlIdentifier("app")), CurrentDatabase());

        // Assert
        result.Value!.Schemas.ShouldHaveSingleItem().Name.ShouldBe("app");
        result.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>
    /// The difference that removes app's status enum, and nothing else.
    /// </summary>
    private static DatabaseDiff EnumRemovalDiff() => new(
    [
        new SchemaDiff(new SqlIdentifier("app"), Enums: [new EnumDiff(new SqlIdentifier("app"), new SqlIdentifier("status"), ChangeKind.Remove)]),
    ]);

    /// <summary>
    /// app.status (an enum), with billing.orders.state declared against it — qualified or bare.
    /// </summary>
    private static Database DatabaseWithEnumTypedColumn(SqlType columnType) => new()
    {
        Schemas = [
        new Schema { Name = new SqlIdentifier("app"), Enums = [new EnumType { Name = new SqlIdentifier("status"), Values = ["new", "done"] }] },
        new Schema { Name = new SqlIdentifier("billing"),
            Tables = [new Table { Name = new SqlIdentifier("orders"), Columns = [new Column { Name = new SqlIdentifier("state"), Type = columnType }] }] },
    ],
    };

    [Fact]
    public void ScopedTo_RemovalAnOutOfScopeColumnDependsOn_Blocks_InsteadOfWidening()
    {
        // Arrange — closure severs definitions, never data: a column stands for its table's rows, so there is
        // no minimal sever. The plan is blocked, and still carried for review.
        var current = DatabaseWithEnumTypedColumn(SqlType.Custom(new SqlIdentifier("app"), "status"));

        // Act
        var result = EnumRemovalDiff().ScopedTo(PlanningScope.To(new SqlIdentifier("app")), current);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Value!.Schemas.ShouldHaveSingleItem().Name.ShouldBe("app"); // nothing widened into billing
        result.Diagnostics.ShouldHaveSingleItem().ShouldBe(
            DiffDiagnostics.ColumnBlocksRemoval([new MemberAddress(new SqlIdentifier("billing"), new SqlIdentifier("orders"), new SqlIdentifier("state"))]));
    }

    [Fact]
    public void ScopedTo_RemovalABareTypedColumnAppearsToDependOn_Hedges_InsteadOfBlocking()
    {
        // Arrange — the edge was bound by name alone, and a wrong guess must not block a plan that need not
        // be. The plan proceeds with a warning; if the guess was right, the database rejects it at apply.
        var current = DatabaseWithEnumTypedColumn(SqlType.Custom("status"));

        // Act
        var result = EnumRemovalDiff().ScopedTo(PlanningScope.To(new SqlIdentifier("app")), current);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Schemas.ShouldHaveSingleItem().Name.ShouldBe("app");
        result.Diagnostics.ShouldHaveSingleItem().ShouldBe(
            DiffDiagnostics.InferredColumnMayBlockRemoval([new MemberAddress(new SqlIdentifier("billing"), new SqlIdentifier("orders"), new SqlIdentifier("state"))]));
    }

    [Fact]
    public void ScopedTo_RemovalAnOutOfScopeDomainIsBuiltOn_DropsTheDomain()
    {
        // Arrange — a domain is a definition, so it severs the way a view does.
        var current = new Database
        {
            Schemas = [
            new Schema { Name = new SqlIdentifier("app"), Enums = [new EnumType { Name = new SqlIdentifier("status"), Values = ["new", "done"] }] },
            new Schema { Name = new SqlIdentifier("billing"),
                Domains = [new DomainType { Name = new SqlIdentifier("tracked_status"), DataType = SqlType.Custom(new SqlIdentifier("app"), "status") }] },
        ],
        };

        // Act
        var result = EnumRemovalDiff().ScopedTo(PlanningScope.To(new SqlIdentifier("app")), current);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var billing = result.Value!.Schemas.Single(s => s.Name == new SqlIdentifier("billing"));
        billing.Kind.ShouldBeNull();
        billing.Domains.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            d => d.Name.ShouldBe("tracked_status"),
            d => d.Kind.ShouldBe(ChangeKind.Remove));
        result.Diagnostics.ShouldHaveSingleItem().ShouldBe(
            DiffDiagnostics.SeveredOutOfScope([new ObjectAddress(new SqlIdentifier("billing"), new SqlIdentifier("tracked_status"))]));
    }
}
