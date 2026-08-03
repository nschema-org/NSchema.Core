using NSchema.Model;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Project.Serialization.Nsql;

public sealed class RoutineDependencyExtractorTests
{
    private static List<ObjectAddress> Extract(string definition) =>
        RoutineDependencyExtractor.Extract(definition, "app");

    [Fact]
    public void Extract_CollectsScalarCallsOutsideFromClauses()
    {
        // Act — the Pagila shape: a call in a WHERE clause, which a FROM/JOIN scan alone misses.
        var result = Extract("RETURNS SETOF integer LANGUAGE sql AS $$ SELECT inventory_id FROM inventory WHERE inventory_in_stock(inventory_id) $$");

        // Assert
        result.ShouldContain(new ObjectAddress("app", "inventory_in_stock"));
        result.ShouldContain(new ObjectAddress("app", "inventory")); // the FROM target still collects
    }

    [Fact]
    public void Extract_ResolvesAQualifiedCallAgainstItsWrittenSchema()
    {
        // Act
        var result = Extract("RETURNS integer LANGUAGE sql AS $$ SELECT billing.net_total(id) $$");

        // Assert
        result.ShouldContain(new ObjectAddress("billing", "net_total"));
    }

    [Fact]
    public void Extract_OverCollectsBuiltinsHarmlessly()
    {
        // Act — count( is collected like any call site; it names no planned object, so it costs nothing.
        var result = Extract("RETURNS integer LANGUAGE sql AS $$ SELECT count(*) FROM orders $$");

        // Assert
        result.ShouldContain(new ObjectAddress("app", "count"));
        result.ShouldContain(new ObjectAddress("app", "orders"));
    }

    [Fact]
    public void Extract_CollectsEachReferenceOnce()
    {
        // Act
        var result = Extract("AS $$ SELECT f(1), f(2), f(3) $$");

        // Assert
        result.Count(a => a == new ObjectAddress("app", "f")).ShouldBe(1);
    }
}
