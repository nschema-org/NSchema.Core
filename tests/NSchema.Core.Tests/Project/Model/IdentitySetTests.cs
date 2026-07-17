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

    private static ObjectIdentity Table(string name) => new(ObjectKind.Table, new ObjectAddress(_app, new SqlIdentifier(name)));

    [Fact]
    public void Contains_MatchesByValue_PerLevel()
    {
        // Arrange
        var set = new IdentitySet(
            Schemas: [_app],
            Objects: [Table("users")],
            Extensions: [new SqlIdentifier("citext")]);

        // Assert
        set.ContainsSchema(new SqlIdentifier("APP")).ShouldBeTrue(); // identifiers compare case-insensitively
        set.ContainsObject(Table("users")).ShouldBeTrue();
        set.ContainsObject(new ObjectIdentity(ObjectKind.View, new ObjectAddress(_app, new SqlIdentifier("users")))).ShouldBeFalse(); // same address, different kind
        set.ContainsExtension(new SqlIdentifier("citext")).ShouldBeTrue();
    }

    [Fact]
    public void Union_MergesDistinct()
    {
        // Arrange
        var left = new IdentitySet(Schemas: [_app], Objects: [Table("users")]);
        var right = new IdentitySet(Schemas: [_app], Objects: [Table("orders")]);

        // Act
        var union = left.Union(right);

        // Assert
        union.Schemas.ShouldBe([_app]);
        union.Objects.ShouldBe([Table("users"), Table("orders")]);
    }

    [Fact]
    public void Except_RemovesByValue()
    {
        // Arrange
        var set = new IdentitySet(Schemas: [_app], Objects: [Table("users"), Table("orders")]);

        // Act
        var remaining = set.Except(new IdentitySet(Objects: [Table("users")]));

        // Assert
        remaining.Schemas.ShouldBe([_app]);
        remaining.Objects.ShouldBe([Table("orders")]);
    }

    [Fact]
    public void Schema_Construction_StampsItselfOntoContainedObjects()
    {
        // Arrange & Act — the object arrives bare; the containing schema completes its identity.
        var schema = new Schema(_app, tables: [new Table(new SqlIdentifier("users"))]);

        // Assert
        var table = schema.Tables.ShouldHaveSingleItem();
        table.Schema.ShouldBeSameAs(schema);
        table.Identity.ShouldBe(Table("users"));
    }

    [Fact]
    public void Schema_AttachingAnObjectThatBelongsElsewhere_Throws()
    {
        // Arrange — a node has exactly one parent; moving it is refused, never silently copied.
        var table = new Table(new SqlIdentifier("users"));
        _ = new Schema(_app, tables: [table]);

        // Act & Assert
        var attach = () => new Schema(new SqlIdentifier("other"), tables: [table]);
        attach.ShouldThrow<InvalidOperationException>().Message.ShouldContain("already belongs to schema 'app'");
    }

    [Fact]
    public void Table_AttachingAMemberThatBelongsElsewhere_Throws()
    {
        // Arrange
        var column = new Column(new SqlIdentifier("id"), SqlType.Int);
        _ = new Table(new SqlIdentifier("users"), columns: [column]);

        // Act & Assert
        var attach = () => new Table(new SqlIdentifier("orders"), columns: [column]);
        attach.ShouldThrow<InvalidOperationException>().Message.ShouldContain("already belongs to 'users'");
    }

    [Fact]
    public void Schema_With_CopiesTheObjectsItIncorporates()
    {
        // Arrange — With is the explicit copy operation: the copy owns fresh nodes, the source keeps its own.
        var schema = new Schema(_app, tables: [new Table(new SqlIdentifier("users"))]);

        // Act
        var copy = schema.With(tables: schema.Tables);

        // Assert
        copy.Tables.ShouldHaveSingleItem().ShouldNotBeSameAs(schema.Tables[0]);
        copy.Tables[0].Schema.ShouldBeSameAs(copy);
        schema.Tables[0].Schema.ShouldBeSameAs(schema);
    }

    [Fact]
    public void Scope_Covers_ImpliesDownwardThroughContainment()
    {
        // A scope is a cover, not an enumeration: covering a schema covers everything inside it.
        var scope = PlanningScope.To(_app);

        scope.Contains(_app).ShouldBeTrue();
        scope.Contains(Table("users")).ShouldBeTrue();
        scope.Contains(new ObjectIdentity(ObjectKind.Table, new ObjectAddress(new SqlIdentifier("other"), new SqlIdentifier("users")))).ShouldBeFalse();
    }

    [Fact]
    public void CoveredBy_ProjectsTheCoverOntoTheSet_KeepingGlobals()
    {
        // The bridge between the two surfaces: the extensional record filtered by the intensional cover.
        // Extensions are database-global, so every scope covers them.
        var set = new IdentitySet(
            Schemas: [_app, new SqlIdentifier("other")],
            Objects: [Table("users"), new ObjectIdentity(ObjectKind.Table, new ObjectAddress(new SqlIdentifier("other"), new SqlIdentifier("t")))],
            Extensions: [new SqlIdentifier("citext")]);

        var covered = set.CoveredBy(PlanningScope.To(_app));

        covered.Schemas.ShouldBe([_app]);
        covered.Objects.ShouldBe([Table("users")]);
        covered.Extensions.ShouldBe([new SqlIdentifier("citext")]);
    }

    [Fact]
    public void ScopedTo_IsFilteringByTheCover()
    {
        // One tree filter serves both surfaces: scoping a database is filtering it to the covered identities.
        var database = new Database(
            [new Schema(_app, tables: [new Table(new SqlIdentifier("users"))]), new Schema(new SqlIdentifier("other"))],
            extensions: [new Extension(new SqlIdentifier("citext"))]);

        var scoped = database.ScopedTo(PlanningScope.To(_app));

        scoped.Schemas.ShouldHaveSingleItem().Name.ShouldBe(_app);
        scoped.Schemas[0].Tables.ShouldHaveSingleItem().Name.ShouldBe("users");
        scoped.Extensions.ShouldHaveSingleItem().Name.ShouldBe("citext");
    }

    [Fact]
    public void Database_Identities_CoversEveryLevel()
    {
        // Arrange
        var database = new Database(
            [new Schema(_app, tables: [new Table(new SqlIdentifier("users"))], views: [new View(new SqlIdentifier("v"), new SqlText("SELECT 1"))])],
            extensions: [new Extension(new SqlIdentifier("citext"))]);

        // Act
        var identities = database.Identities();

        // Assert
        identities.Schemas.ShouldBe([_app]);
        identities.Objects.ShouldBe([Table("users"), new ObjectIdentity(ObjectKind.View, new ObjectAddress(_app, new SqlIdentifier("v")))], ignoreOrder: true);
        identities.Extensions.ShouldBe([new SqlIdentifier("citext")]);
    }

    [Fact]
    public void Database_FilteredTo_KeepsOnlyMatchingIdentities()
    {
        // Arrange
        var database = new Database(
        [
            new Schema(_app, tables: [new Table(new SqlIdentifier("mine")), new Table(new SqlIdentifier("theirs"))]),
            new Schema(new SqlIdentifier("other")),
        ],
        extensions: [new Extension(new SqlIdentifier("citext")), new Extension(new SqlIdentifier("plpgsql"))]);

        // Act
        var filtered = database.FilteredTo(new IdentitySet(
            Schemas: [_app],
            Objects: [Table("mine")],
            Extensions: [new SqlIdentifier("citext")]));

        // Assert — the unmatched schema, table, and extension are all gone.
        var schema = filtered.Schemas.ShouldHaveSingleItem();
        schema.Name.ShouldBe(_app);
        schema.Tables.ShouldHaveSingleItem().Name.ShouldBe("mine");
        filtered.Extensions.ShouldHaveSingleItem().Name.ShouldBe("citext");
    }
}
