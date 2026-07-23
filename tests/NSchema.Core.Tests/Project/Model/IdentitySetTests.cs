using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Extensions;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Model.Views;

namespace NSchema.Tests.Project.Model;

public sealed class IdentitySetTests
{
    private static readonly SqlIdentifier _app = new("app");

    private static ObjectAddress Table(string name) => new(_app, name, ObjectKind.Table);

    [Fact]
    public void Contains_MatchesByValue_PerLevel()
    {
        // Arrange
        var set = new IdentitySet(
            Schemas: [new SchemaAddress(_app)],
            Objects: [Table("users")],
            Extensions: [new ScopedAddress(null, "citext")]);

        // Assert
        set.ContainsSchema("app").ShouldBeTrue();
        set.ContainsSchema("APP").ShouldBeFalse(); // identifiers are case-sensitive
        set.ContainsObject(Table("users")).ShouldBeTrue();
        set.ContainsObject(new ObjectAddress(_app, "users") with { Kind = ObjectKind.View }).ShouldBeFalse(); // same address, different kind
        set.ContainsExtension("citext").ShouldBeTrue();
    }

    [Fact]
    public void Union_MergesDistinct()
    {
        // Arrange
        var left = new IdentitySet(Schemas: [new SchemaAddress(_app)], Objects: [Table("users")]);
        var right = new IdentitySet(Schemas: [new SchemaAddress(_app)], Objects: [Table("orders")]);

        // Act
        var union = left.Union(right);

        // Assert
        union.Schemas.Select(s => s.Schema).ShouldBe([_app]);
        union.Objects.ShouldBe([Table("users"), Table("orders")]);
    }

    [Fact]
    public void Except_RemovesByValue()
    {
        // Arrange
        var set = new IdentitySet(Schemas: [new SchemaAddress(_app)], Objects: [Table("users"), Table("orders")]);

        // Act
        var remaining = set.Except(new IdentitySet(Objects: [Table("users")]));

        // Assert
        remaining.Schemas.Select(s => s.Schema).ShouldBe([_app]);
        remaining.Objects.ShouldBe([Table("orders")]);
    }

    [Fact]
    public void Schema_Construction_StampsItselfOntoContainedObjects()
    {
        // Arrange & Act — the object arrives bare; the containing schema completes its identity.
        var schema = new Schema { Name = _app, Tables = [new Table { Name = "users" }] };

        // Assert
        var table = schema.Tables.ShouldHaveSingleItem();
        table.Schema.ShouldBeSameAs(schema);
        table.Address.ShouldBe(Table("users"));
    }

    [Fact]
    public void Schema_AttachingAnObjectThatBelongsElsewhere_Throws()
    {
        // Arrange — a node has exactly one parent; moving it is refused, never silently copied.
        var table = new Table { Name = "users" };
        _ = new Schema { Name = _app, Tables = [table] };

        // Act & Assert
        var attach = () => new Schema { Name = "other", Tables = [table] };
        attach.ShouldThrow<InvalidOperationException>().Message.ShouldContain("already belongs to schema 'app'");
    }

    [Fact]
    public void Table_AttachingAMemberThatBelongsElsewhere_Throws()
    {
        // Arrange
        var column = new Column { Name = "id", Type = SqlType.Int };
        _ = new Table { Name = "users", Columns = [column] };

        // Act & Assert
        var attach = () => new Table { Name = "orders", Columns = [column] };
        attach.ShouldThrow<InvalidOperationException>().Message.ShouldContain("already belongs to 'users'");
    }

    [Fact]
    public void Schema_Clone_CopiesTheObjectsItIncorporates()
    {
        // Arrange — Clone is the explicit copy operation: the copy owns fresh nodes, the source keeps its own.
        var schema = new Schema { Name = _app, Tables = [new Table { Name = "users" }] };

        // Act
        var copy = schema.Clone();

        // Assert
        copy.Tables.ShouldHaveSingleItem().ShouldNotBeSameAs(schema.Tables[0]);
        copy.Tables[0].Schema.ShouldBeSameAs(copy);
        schema.Tables[0].Schema.ShouldBeSameAs(schema);
    }

    [Fact]
    public void Scope_Covers_ImpliesDownwardThroughContainment()
    {
        // A scope is a cover, not an enumeration: covering a schema covers everything inside it.
        var scope = PlanningScope.To(new SchemaAddress(_app));

        scope.Contains(_app).ShouldBeTrue();
        scope.Contains(Table("users")).ShouldBeTrue();
        scope.Contains(new ObjectAddress("other", "users") with { Kind = ObjectKind.Table }).ShouldBeFalse();
    }

    [Fact]
    public void CoveredBy_ProjectsTheCoverOntoTheSet_KeepingGlobals()
    {
        // The bridge between the two surfaces: the extensional record filtered by the intensional cover.
        // Extensions are database-global, so every scope covers them.
        var set = new IdentitySet(
            Schemas: [new SchemaAddress(_app), new SchemaAddress("other")],
            Objects: [Table("users"), new ObjectAddress("other", "t") with { Kind = ObjectKind.Table }],
            Extensions: [new ScopedAddress(null, "citext")]);

        var covered = set.CoveredBy(PlanningScope.To(new SchemaAddress(_app)));

        covered.Schemas.Select(s => s.Schema).ShouldBe([_app]);
        covered.Objects.ShouldBe([Table("users")]);
        covered.Extensions.Select(e => e.Name).ShouldBe(["citext"]);
    }

    [Fact]
    public void CoveredBy_ObjectEntry_CoversTheObject_NotItsSchema()
    {
        // The managed-set math depends on this: targeting an object must not claim (or release) management
        // of its schema container. Extensions stay database-global, covered by every scope.
        var set = new IdentitySet(
            Schemas: [new SchemaAddress(_app)],
            Objects: [Table("users"), Table("orders")],
            Extensions: [new ScopedAddress(null, "citext")]);

        var covered = set.CoveredBy(PlanningScope.To([new ObjectAddress(_app, "users")]));

        covered.Schemas.ShouldBeEmpty();
        covered.Objects.ShouldBe([Table("users")]);
        covered.Extensions.Select(e => e.Name).ShouldBe(["citext"]);
    }

    [Fact]
    public void ScopedTo_IsFilteringByTheCover()
    {
        // One tree filter serves both surfaces: scoping a database is filtering it to the covered identities.
        var database = new Database
        {
            Schemas = [new Schema { Name = _app, Tables = [new Table { Name = "users" }] }, new Schema { Name = "other" }],
            Extensions = [new Extension { Name = "citext" }],
        };

        var scoped = database.ScopedTo(PlanningScope.To(new SchemaAddress(_app)));

        scoped.Schemas.ShouldHaveSingleItem().Name.ShouldBe(_app);
        scoped.Schemas[0].Tables.ShouldHaveSingleItem().Name.ShouldBe("users");
        scoped.Extensions.ShouldHaveSingleItem().Name.ShouldBe("citext");
    }

    [Fact]
    public void Database_Identities_CoversEveryLevel()
    {
        // Arrange
        var database = new Database
        {
            Schemas = [new Schema { Name = _app, Tables = [new Table { Name = "users" }], Views = [new View { Name = "v", Body = "SELECT 1" }] }],
            Extensions = [new Extension { Name = "citext" }],
        };

        // Act
        var identities = database.Identities();

        // Assert
        identities.Schemas.Select(s => s.Schema).ShouldBe([_app]);
        identities.Objects.ShouldBe([Table("users"), new ObjectAddress(_app, "v") with { Kind = ObjectKind.View }], ignoreOrder: true);
        identities.Extensions.Select(e => e.Name).ShouldBe(["citext"]);
    }

    [Fact]
    public void Database_FilteredTo_KeepsOnlyMatchingIdentities()
    {
        // Arrange
        var database = new Database
        {
            Schemas = [
            new Schema { Name = _app, Tables = [new Table { Name = "mine" }, new Table { Name = "theirs" }] },
            new Schema { Name = "other" },
        ],
            Extensions = [new Extension { Name = "citext" }, new Extension { Name = "plpgsql" }],
        };

        // Act
        var filtered = database.FilteredTo(new IdentitySet(
            Schemas: [new SchemaAddress(_app)],
            Objects: [Table("mine")],
            Extensions: [new ScopedAddress(null, "citext")]));

        // Assert — the unmatched schema, table, and extension are all gone.
        var schema = filtered.Schemas.ShouldHaveSingleItem();
        schema.Name.ShouldBe(_app);
        schema.Tables.ShouldHaveSingleItem().Name.ShouldBe("mine");
        filtered.Extensions.ShouldHaveSingleItem().Name.ShouldBe("citext");
    }
}
