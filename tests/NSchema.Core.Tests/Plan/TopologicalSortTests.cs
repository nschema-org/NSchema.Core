using NSchema.Plan.Domain;

namespace NSchema.Tests.Plan;

public sealed class TopologicalSortTests
{
    private sealed record Node(string Name, params string[] Deps);

    private static List<string> Order(params Node[] nodes) =>
        TopologicalSort.Order(nodes, n => n.Name, n => n.Deps, n => n.Name)
            .Select(n => n.Name).ToList();

    [Fact]
    public void Order_PutsDependenciesFirst()
    {
        // b depends on a, c depends on b -> a, b, c regardless of input order.
        Order(new Node("c", "b"), new Node("b", "a"), new Node("a")).ShouldBe(["a", "b", "c"]);
    }

    [Fact]
    public void Order_IgnoresDependenciesOutsideTheSet()
    {
        // a depends on "external" which isn't in the set -> no edge, original order kept.
        Order(new Node("a", "external"), new Node("b")).ShouldBe(["a", "b"]);
    }

    [Fact]
    public void Order_IsStableForIndependentItems()
    {
        Order(new Node("x"), new Node("y"), new Node("z")).ShouldBe(["x", "y", "z"]);
    }

    [Fact]
    public void Order_DiamondDependency()
    {
        // d depends on b and c; b and c depend on a -> a before b,c before d.
        var ordered = Order(new Node("d", "b", "c"), new Node("b", "a"), new Node("c", "a"), new Node("a"));
        ordered.ShouldContain("a");
        ordered.IndexOf("a").ShouldBeLessThan(ordered.IndexOf("b"));
        ordered.IndexOf("a").ShouldBeLessThan(ordered.IndexOf("c"));
        ordered.IndexOf("b").ShouldBeLessThan(ordered.IndexOf("d"));
        ordered.IndexOf("c").ShouldBeLessThan(ordered.IndexOf("d"));
    }

    [Fact]
    public void Order_DetectsCycle()
    {
        var ex = Should.Throw<InvalidOperationException>(() => Order(new Node("a", "b"), new Node("b", "a")));
        ex.Message.ShouldContain("cycle", Case.Insensitive);
    }
}
