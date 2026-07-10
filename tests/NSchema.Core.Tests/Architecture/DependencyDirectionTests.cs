using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static NSchema.Tests.Architecture.ArchitectureTestSupport;

namespace NSchema.Tests.Architecture;

/// <summary>
/// Guards the dependency direction between the feature namespaces: the domain pipeline flows
/// Schema ← Diff ← Plan, shared leaves (Diagnostics, Helpers, Configuration, Policies) depend on
/// no feature, and nothing outside the application root composes Operations.
/// </summary>
/// <remarks>
/// Known deviation, deliberately not asserted: Plan ⇄ Sql reference each other (the planner and
/// plan files deal in <c>SqlPlan</c>/<c>ScriptHash</c>; the generator contract takes
/// <c>MigrationPlan</c>). The v5 feature-slice audit resolves this by pulling generation toward
/// Plan — tighten the Plan and Sql rows when it lands.
/// </remarks>
public sealed class DependencyDirectionTests
{
    [Theory]
    // Pipeline: each stage may see only the stages before it.
    [InlineData("NSchema.Schema", new[] { "NSchema.Diff", "NSchema.Plan", "NSchema.Sql", "NSchema.State", "NSchema.Operations", "NSchema.Plugins" })]
    [InlineData("NSchema.Diff", new[] { "NSchema.Plan", "NSchema.Sql", "NSchema.State", "NSchema.Operations", "NSchema.Plugins" })]
    [InlineData("NSchema.Plan", new[] { "NSchema.State", "NSchema.Operations", "NSchema.Plugins" })]
    [InlineData("NSchema.Sql", new[] { "NSchema.Schema", "NSchema.Diff", "NSchema.State", "NSchema.Operations", "NSchema.Plugins" })]
    // State captures schemas and script hashes; it never sees planning or orchestration.
    [InlineData("NSchema.State", new[] { "NSchema.Diff", "NSchema.Plan", "NSchema.Operations", "NSchema.Plugins" })]
    // Shared leaves: no feature dependencies at all.
    [InlineData("NSchema.Diagnostics", new[] { "NSchema.Schema", "NSchema.Diff", "NSchema.Plan", "NSchema.Sql", "NSchema.State", "NSchema.Operations", "NSchema.Configuration", "NSchema.Plugins", "NSchema.Policies" })]
    [InlineData("NSchema.Helpers", new[] { "NSchema.Schema", "NSchema.Diff", "NSchema.Plan", "NSchema.Sql", "NSchema.State", "NSchema.Operations", "NSchema.Configuration", "NSchema.Plugins", "NSchema.Policies" })]
    [InlineData("NSchema.Configuration", new[] { "NSchema.Schema", "NSchema.Diff", "NSchema.Plan", "NSchema.Sql", "NSchema.State", "NSchema.Operations", "NSchema.Plugins", "NSchema.Policies" })]
    [InlineData("NSchema.Policies", new[] { "NSchema.Schema", "NSchema.Diff", "NSchema.Plan", "NSchema.Sql", "NSchema.State", "NSchema.Operations", "NSchema.Plugins" })]
    // Plugin contracts describe scaffolding, not the engine.
    [InlineData("NSchema.Plugins", new[] { "NSchema.Schema", "NSchema.Diff", "NSchema.Plan", "NSchema.Sql", "NSchema.State", "NSchema.Operations" })]
    // Operations orchestrates the domain but never the plugin contracts.
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
