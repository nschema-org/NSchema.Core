using NSchema.Model;

namespace NSchema.Tests.Project.Model;

/// <summary>
/// One axis at two granularities: a schema entry covers the schema and everything inside it; an object entry
/// covers that object alone. An entry covers itself and everything below it — nothing above it.
/// </summary>
public sealed class PlanningScopeTests
{
    private static readonly SqlIdentifier App = new("app");

    private static ObjectAddress Address(string schema, string name) => new(schema, name);

    [Fact]
    public void To_EmptyOfBoth_NormalizesToAll()
    {
        PlanningScope.To([], []).ShouldBeSameAs(PlanningScope.All);
        PlanningScope.To(Enumerable.Empty<ObjectAddress>()).ShouldBeSameAs(PlanningScope.All);
    }

    [Fact]
    public void SchemaEntry_CoversTheSchema_AndEverythingInsideIt()
    {
        var scope = PlanningScope.To(App);

        scope.Contains(App).ShouldBeTrue();
        scope.Contains(Address("app", "users")).ShouldBeTrue();
        scope.Contains(new ObjectIdentity(ObjectKind.Table, Address("app", "users"))).ShouldBeTrue();
    }

    [Fact]
    public void ObjectEntry_CoversTheObjectAlone_NotItsSchema()
    {
        // Membership implies downward through containment, never upward: targeting an object says nothing
        // about its schema's other contents or the schema's own facets.
        var scope = PlanningScope.To([Address("app", "users")]);

        scope.Contains(Address("app", "users")).ShouldBeTrue();
        scope.Contains(App).ShouldBeFalse();
        scope.Contains(Address("app", "orders")).ShouldBeFalse();
    }

    [Fact]
    public void ObjectEntry_IsKindFree()
    {
        // A scope entry is an address; which kinds may share a location is engine-specific, so every kind
        // at the address is covered.
        var scope = PlanningScope.To([Address("app", "users")]);

        scope.Contains(new ObjectIdentity(ObjectKind.Table, Address("app", "users"))).ShouldBeTrue();
        scope.Contains(new ObjectIdentity(ObjectKind.View, Address("app", "users"))).ShouldBeTrue();
    }

    [Fact]
    public void ObjectEntry_InsideANamedSchema_IsAbsorbed()
    {
        var scope = PlanningScope.To([App], [Address("app", "users"), Address("billing", "orders")]);

        scope.Objects.ShouldHaveSingleItem().ShouldBe(Address("billing", "orders"));
        scope.Schemas.ShouldBe([App]);
    }

    [Fact]
    public void SchemaNames_ProjectsTheObjectSchemas_ForTheReadSeams()
    {
        // The read seams take schema names; an object entry still means its schema must be read.
        var scope = PlanningScope.To([App], [Address("billing", "orders")]);

        scope.SchemaNames.ShouldBe([App, "billing"]);
        scope.Schemas.ShouldBe([App]);
        scope.IsUnscoped.ShouldBeFalse();
    }

    [Fact]
    public void Contains_IsCaseSensitive_AtBothGranularities()
    {
        var scope = PlanningScope.To([App], [Address("billing", "orders")]);

        scope.Contains("APP").ShouldBeFalse();
        scope.Contains(Address("Billing", "Orders")).ShouldBeFalse();
        scope.Contains(App).ShouldBeTrue();
        scope.Contains(Address("billing", "orders")).ShouldBeTrue();
    }
}
