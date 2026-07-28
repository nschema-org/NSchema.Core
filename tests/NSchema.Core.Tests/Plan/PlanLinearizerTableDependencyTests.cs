using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Schemas;
using NSchema.Diff.Domain.Tables;
using NSchema.Model;
using NSchema.Model.Tables;
using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Services;
using NSchema.Plan.Domain.Tables;
using NSchema.Tests.Helpers;

namespace NSchema.Tests.Plan;

/// <summary>
/// Pins the order the linearizer gives table creates and drops: a table is created after the tables its
/// foreign keys point at, and dropped before them. The foreign keys are read from the databases either side of
/// the migration, so each table under test is declared on the side the change comes from.
/// </summary>
public sealed class PlanLinearizerTableDependencyTests
{
    private readonly PlanLinearizer _linearizer = new();
    private readonly MigrationSides _sides = new();

    // -- builders --------------------------------------------------------------

    /// <summary>A table whose foreign keys point at each of <paramref name="references"/> ("schema.table").</summary>
    private static Table TableReferencing(string name, params string[] references) => new()
    {
        Name = name,
        ForeignKeys = [.. references.Select((reference, i) => new ForeignKey
        {
            Name = $"fk_{name}_{i}",
            ColumnNames = ["id"],
            References = Address(reference),
            ReferencedColumnNames = ["id"],
        })],
    };

    private static ObjectAddress Address(string qualified) =>
        qualified.Split('.') is [var schema, var name] ? new ObjectAddress(schema, name) : new ObjectAddress("app", qualified);

    /// <summary>A table the project declares and the database does not.</summary>
    private TableDiff Added(string schema, string name, params string[] references) =>
        TableDiff.Added(schema, _sides.Creating(schema, TableReferencing(name, references)));

    /// <summary>A table the database has and the project does not.</summary>
    private TableDiff Removed(string schema, string name, params string[] references)
    {
        _sides.Dropping(schema, TableReferencing(name, references));
        return TableDiff.Removed(schema, name);
    }

    private IReadOnlyList<MigrationAction> Linearize(params SchemaDiff[] schemas) =>
        _linearizer.Linearize(new DatabaseDiff(schemas), _sides.Dependencies);

    private static IReadOnlyList<string> DropOrder(IReadOnlyList<MigrationAction> plan) =>
        [.. plan.OfType<DropTable>().Select(d => d.Table.Name.Value)];

    private static IReadOnlyList<string> CreateOrder(IReadOnlyList<MigrationAction> plan) =>
        [.. plan.OfType<CreateTable>().Select(c => c.Table.Name.Value)];

    // -------------------------------------------------------------------------

    [Fact]
    public void DroppedTables_AreOrderedDependentsFirst()
    {
        // Arrange
        var schema = SchemaDiff.Removed("identity") with
        {
            Tables = [Removed("identity", "permissions"), Removed("identity", "role_permissions", "identity.permissions")],
        };

        // Act
        var plan = Linearize(schema);

        // Assert
        DropOrder(plan).ShouldBe(["role_permissions", "permissions"]);
    }

    [Fact]
    public void DroppedTables_AreOrderedAcrossSchemas()
    {
        // Arrange
        var identity = SchemaDiff.Removed("identity") with { Tables = [Removed("identity", "users")] };
        var staff = SchemaDiff.Removed("staff") with { Tables = [Removed("staff", "employees", "identity.users")] };

        // Act
        var plan = Linearize(identity, staff);

        // Assert
        DropOrder(plan).ShouldBe(["employees", "users"]);
    }

    [Fact]
    public void DroppedTables_WithoutDependencies_KeepTheirDeclaredOrder()
    {
        // Arrange
        var schema = SchemaDiff.Removed("app") with
        {
            Tables = [Removed("app", "alpha"), Removed("app", "beta"), Removed("app", "gamma")],
        };

        // Act
        var plan = Linearize(schema);

        // Assert
        DropOrder(plan).ShouldBe(["alpha", "beta", "gamma"]);
    }

    [Fact]
    public void CreatedTables_AreOrderedDependenciesFirst()
    {
        // Arrange
        var schema = SchemaDiff.Added("clients") with
        {
            Tables = [Added("clients", "insurer_references", "clients.insurers"), Added("clients", "insurers")],
        };

        // Act
        var plan = Linearize(schema);

        // Assert
        CreateOrder(plan).ShouldBe(["insurers", "insurer_references"]);
    }

    [Fact]
    public void CreatedTables_AreOrderedAcrossSchemas()
    {
        // Arrange
        var staff = SchemaDiff.Added("staff") with { Tables = [Added("staff", "employees", "identity.users")] };
        var identity = SchemaDiff.Added("identity") with { Tables = [Added("identity", "users")] };

        // Act
        var plan = Linearize(staff, identity);

        // Assert
        CreateOrder(plan).ShouldBe(["users", "employees"]);
    }

    [Fact]
    public void SelfReferencingTable_IsOrderedWithoutCycling()
    {
        // Arrange
        var schema = SchemaDiff.Removed("app") with { Tables = [Removed("app", "nodes", "app.nodes")] };

        // Act
        var plan = Linearize(schema);

        // Assert
        DropOrder(plan).ShouldBe(["nodes"]);
    }

    [Fact]
    public void MutuallyReferencingTables_AreStillPlanned()
    {
        // Arrange
        var schema = SchemaDiff.Removed("app") with
        {
            Tables = [Removed("app", "left", "app.right"), Removed("app", "right", "app.left")],
        };

        // Act
        var plan = Linearize(schema);

        // Assert
        DropOrder(plan).Count.ShouldBe(2);
    }

    [Fact]
    public void TablesTheMigrationDoesNotTouch_DoNotOrderAnything()
    {
        // Arrange — the referenced table stays, so only the referencing one is in the plan to be ordered.
        _sides.Dropping("app", TableReferencing("kept"));
        var schema = SchemaDiff.Containing("app") with { Tables = [Removed("app", "orders", "app.kept")] };

        // Act
        var plan = Linearize(schema);

        // Assert
        DropOrder(plan).ShouldBe(["orders"]);
    }
}
