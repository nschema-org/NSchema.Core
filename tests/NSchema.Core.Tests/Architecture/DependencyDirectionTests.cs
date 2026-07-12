using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static NSchema.Tests.Architecture.ArchitectureTestSupport;

namespace NSchema.Tests.Architecture;

/// <summary>
/// Guards the dependency direction between the clusters: the sources (Project, Current) reference no
/// stage and not each other's machinery; the stages flow Diff ← Plan ← Apply, referencing only earlier
/// stages and source vocabulary; Plugins is the contract between plugin authors and hosts and sees no
/// engine cluster; nothing outside the application root composes Operations.
/// </summary>
public sealed class DependencyDirectionTests
{
    [Theory]
    // Sources: no stages, no orchestration, no plugin contracts, not each other.
    [InlineData("NSchema.Project", new[] { "NSchema.Current", "NSchema.Diff", "NSchema.Plan", "NSchema.Apply", "NSchema.Operations", "NSchema.Plugins" })]
    // Current may read the shared vocabulary (Project.Domain.Models) but never the project language or policies.
    [InlineData("NSchema.Current", new[] { "NSchema.Project.Ddl", "NSchema.Project.Policies", "NSchema.Diff", "NSchema.Plan", "NSchema.Apply", "NSchema.Operations", "NSchema.Plugins" })]
    // Pipeline: each stage may see only the stages before it (and the sources' vocabulary).
    [InlineData("NSchema.Diff", new[] { "NSchema.Plan", "NSchema.Apply", "NSchema.Operations", "NSchema.Plugins" })]
    [InlineData("NSchema.Plan", new[] { "NSchema.Apply", "NSchema.Operations", "NSchema.Plugins" })]
    [InlineData("NSchema.Apply", new[] { "NSchema.Operations", "NSchema.Plugins" })]
    // Plugin contracts describe scaffolding, not the engine.
    // Plugin contracts may speak engine vocabulary (a parsed ConfigBlock), never engine machinery.
    [InlineData("NSchema.Plugins", new[] { "NSchema.Project.Domain", "NSchema.Project.Policies", "NSchema.Current", "NSchema.Diff", "NSchema.Plan", "NSchema.Apply", "NSchema.Operations" })]
    // Operations orchestrates the engine but never the plugin contracts.
    [InlineData("NSchema.Operations", new[] { "NSchema.Plugins" })]
    public void Namespace_DoesNotDependOnForbiddenNamespaces(string source, string[] forbidden)
    {
        // Arrange
        var rule = Types().That().ResideInNamespaceMatching(Subtree(source))
            .Should().NotDependOnAny(Types().That().ResideInNamespaceMatching(Subtree(forbidden)));

        // Assert
        rule.ShouldBeSatisfied();
    }

    // Every feature namespace above forbids NSchema.Operations, so only the application root
    // (NSchemaApplication and its builder) may compose the operation slices — no extra rule needed.
}
