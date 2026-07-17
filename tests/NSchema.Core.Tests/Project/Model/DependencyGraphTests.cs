using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Schemas;
using NSchema.Model.Services;
using NSchema.Model.Tables;
using NSchema.Model.Views;

namespace NSchema.Tests.Project.Model;

/// <summary>
/// The dependency graph: one edge meaning ("requires, to exist"), walkable both ways. The walks are the
/// callers' business — the graph only knows the edges.
/// </summary>
public class DependencyGraphTests
{
    private static SqlIdentifier Id(string value) => new(value);

    private static DependencyNode Table(string schema, string table) =>
        new(new ObjectAddress(Id(schema), Id(table)), DependencyKind.Table);

    private static DependencyNode View(string schema, string view) =>
        new(new ObjectAddress(Id(schema), Id(view)), DependencyKind.View);

    private static DependencyNode ForeignKey(string schema, string table, string name) =>
        new(new MemberAddress(Id(schema), Id(table), Id(name)), DependencyKind.ForeignKey);

    private static Table WithForeignKey(string name, string constraint, string toSchema, string toTable) =>
        new(Id(name), Columns: [new Column(Id("id"), SqlType.Int)])
        {
            ForeignKeys = [new ForeignKey(Id(constraint), [Id("id")], Id(toSchema), Id(toTable), [Id("id")])],
        };

    /// <summary>app.users, and billing.orders pointing an FK at it.</summary>
    private static Database CrossSchemaForeignKey() => new(
    [
        new Schema(Id("app"), Tables: [new Table(Id("users"), Columns: [new Column(Id("id"), SqlType.Int)])]),
        new Schema(Id("billing"), Tables: [WithForeignKey("orders", "fk_orders_user", "app", "users")]),
    ]);

    [Fact]
    public void DependentsOf_ReferencedTable_IsTheConstraint_NotTheTableThatOwnsIt()
    {
        // Arrange — the case a partial teardown hits: dropping app.users orphans billing.orders' FK. What
        // must go is the constraint; billing.orders keeps its rows.
        var graph = new DependencyGraph(CrossSchemaForeignKey());

        // Act
        var dependents = graph.DependentsOf(Table("app", "users"));

        // Assert
        dependents.ShouldHaveSingleItem().ShouldBe(ForeignKey("billing", "orders", "fk_orders_user"));
    }

    [Fact]
    public void DependentsOf_TableReadByAView_IsTheView()
    {
        // Arrange — a view's dependency is embedded in its body, so there is nothing to sever but the view.
        var database = new Database(
        [
            new Schema(Id("app"),
                Tables: [new Table(Id("users"), Columns: [new Column(Id("id"), SqlType.Int)])],
                Views: [new View(Id("active_users"), new SqlText("select * from app.users"),
                    DependsOn: [new ViewDependency(Id("app"), Id("users"))])]),
        ]);
        var graph = new DependencyGraph(database);

        // Act
        var dependents = graph.DependentsOf(Table("app", "users"));

        // Assert
        dependents.ShouldHaveSingleItem().ShouldBe(View("app", "active_users"));
    }

    [Fact]
    public void DependenciesOf_Constraint_IsTheTableItPointsAt_NotItsOwner()
    {
        // Arrange — containment is not an edge, so the FK does not "require" billing.orders.
        var graph = new DependencyGraph(CrossSchemaForeignKey());

        // Act
        var dependencies = graph.DependenciesOf(ForeignKey("billing", "orders", "fk_orders_user"));

        // Assert
        dependencies.ShouldHaveSingleItem().ShouldBe(Table("app", "users"));
    }

    [Fact]
    public void DependentsOf_SomethingNothingNeeds_IsEmpty()
    {
        // Arrange
        var graph = new DependencyGraph(CrossSchemaForeignKey());

        // Act & Assert — the walk terminates: a constraint has no dependents of its own.
        graph.DependentsOf(ForeignKey("billing", "orders", "fk_orders_user")).ShouldBeEmpty();
        graph.DependentsOf(Table("billing", "orders")).ShouldBeEmpty();
    }

    [Fact]
    public void ForeignKey_PointingOutsideTheDatabase_ProducesNoEdge()
    {
        // Arrange — a reference to something not here (unmanaged, or simply absent) is ignored rather than
        // inventing a node, as the linearizer's sort does.
        var database = new Database([new Schema(Id("billing"), Tables: [WithForeignKey("orders", "fk_orders_user", "elsewhere", "users")])]);
        var graph = new DependencyGraph(database);

        // Assert
        graph.DependenciesOf(ForeignKey("billing", "orders", "fk_orders_user")).ShouldBeEmpty();
    }

    [Fact]
    public void SelfReferencingForeignKey_DependsOnItsOwnTable()
    {
        // Arrange — a self-reference is an ordinary edge; cycles are the caller's problem, not the graph's.
        var database = new Database([new Schema(Id("app"), Tables: [WithForeignKey("nodes", "fk_nodes_parent", "app", "nodes")])]);
        var graph = new DependencyGraph(database);

        // Assert
        graph.DependenciesOf(ForeignKey("app", "nodes", "fk_nodes_parent")).ShouldHaveSingleItem().ShouldBe(Table("app", "nodes"));
        graph.DependentsOf(Table("app", "nodes")).ShouldHaveSingleItem().ShouldBe(ForeignKey("app", "nodes", "fk_nodes_parent"));
    }

    [Fact]
    public void AllDependentsOf_CollectsTransitively_ExcludingTheSeeds()
    {
        // Arrange — app.users ← app.active_users (view) ← app.recent_users (view on the view).
        var database = new Database(
        [
            new Schema(Id("app"),
                Tables: [new Table(Id("users"), Columns: [new Column(Id("id"), SqlType.Int)])],
                Views:
                [
                    new View(Id("active_users"), new SqlText("select * from app.users"),
                        DependsOn: [new ViewDependency(Id("app"), Id("users"))]),
                    new View(Id("recent_users"), new SqlText("select * from app.active_users"),
                        DependsOn: [new ViewDependency(Id("app"), Id("active_users"))]),
                ]),
        ]);
        var graph = new DependencyGraph(database);

        // Act — dropping app.users costs both views, not just the one that names it.
        var closure = graph.AllDependentsOf([Table("app", "users")]);

        // Assert
        closure.ShouldBe([View("app", "active_users"), View("app", "recent_users")], ignoreOrder: true);
    }

    [Fact]
    public void AllDependentsOf_MutuallyReferencingTables_CostsOnlyTheConstraintPointingAtTheSeed()
    {
        // Arrange — mutual foreign keys are legal and common. They are only a cycle if containment is an
        // edge (a needs fk_a_b needs b needs fk_b_a needs a); it isn't, so the walk stays finite by shape.
        var database = new Database(
        [
            new Schema(Id("app"), Tables: [WithForeignKey("a", "fk_a_b", "app", "b"), WithForeignKey("b", "fk_b_a", "app", "a")]),
        ]);
        var graph = new DependencyGraph(database);

        // Act
        var closure = graph.AllDependentsOf([Table("app", "a")]);

        // Assert — dropping app.a costs the constraint aimed at it. Its own fk_a_b goes with the table it
        // belongs to, which is the tree's business, not the graph's.
        closure.ShouldHaveSingleItem().ShouldBe(ForeignKey("app", "b", "fk_b_a"));
    }

    [Fact]
    public void AllDependentsOf_ADiamond_CollectsEachNodeOnce()
    {
        // Arrange — two views read app.users and a third reads both, so 'summary' is reachable by two paths.
        var database = new Database(
        [
            new Schema(Id("app"),
                Tables: [new Table(Id("users"), Columns: [new Column(Id("id"), SqlType.Int)])],
                Views:
                [
                    new View(Id("active"), new SqlText("select * from app.users"), DependsOn: [new ViewDependency(Id("app"), Id("users"))]),
                    new View(Id("recent"), new SqlText("select * from app.users"), DependsOn: [new ViewDependency(Id("app"), Id("users"))]),
                    new View(Id("summary"), new SqlText("select * from app.active, app.recent"),
                        DependsOn: [new ViewDependency(Id("app"), Id("active")), new ViewDependency(Id("app"), Id("recent"))]),
                ]),
        ]);
        var graph = new DependencyGraph(database);

        // Act
        var closure = graph.AllDependentsOf([Table("app", "users")]);

        // Assert
        closure.ShouldBe([View("app", "active"), View("app", "recent"), View("app", "summary")], ignoreOrder: true);
    }

    [Fact]
    public void AllDependentsOf_ManySeeds_DoesNotReportOneSeedAsAnothersCost()
    {
        // Arrange — a partial teardown seeds with everything it is already dropping, and wants only the extra.
        var graph = new DependencyGraph(CrossSchemaForeignKey());

        // Act
        var closure = graph.AllDependentsOf([Table("app", "users"), ForeignKey("billing", "orders", "fk_orders_user")]);

        // Assert
        closure.ShouldBeEmpty();
    }

    [Fact]
    public void StatedDependentsOf_ExcludesWhatIsReachedOnlyThroughAnInferredEdge()
    {
        // Arrange — app.users has a foreign key pointing at it (the model states that) and a view reading it
        // (scanned out of the body). The full walk sees both; the stated-only walk sees just the constraint.
        var database = new Database(
        [
            new Schema(Id("app"), Tables: [new Table(Id("users"), Columns: [new Column(Id("id"), SqlType.Int)])],
                Views: [new View(Id("summary"), new SqlText("select * from app.users"), DependsOn: [new ViewDependency(Id("app"), Id("users"))])]),
            new Schema(Id("billing"), Tables: [WithForeignKey("orders", "fk_orders_user", "app", "users")]),
        ]);
        var graph = new DependencyGraph(database);
        var seed = new[] { Table("app", "users") };

        // Assert
        graph.AllDependentsOf(seed).ShouldBe([ForeignKey("billing", "orders", "fk_orders_user"), View("app", "summary")], ignoreOrder: true);
        graph.StatedDependentsOf(seed).ShouldHaveSingleItem().ShouldBe(ForeignKey("billing", "orders", "fk_orders_user"));
    }

    [Fact]
    public void At_FindsNodesByAddress_RegardlessOfKind()
    {
        // Arrange — the seed for a walk is an address; the graph says what is actually there.
        var graph = new DependencyGraph(CrossSchemaForeignKey());

        // Assert
        graph.At(new ObjectAddress(Id("app"), Id("users"))).ShouldHaveSingleItem().ShouldBe(Table("app", "users"));
        graph.At(new ObjectAddress(Id("app"), Id("nope"))).ShouldBeEmpty();
    }
}
