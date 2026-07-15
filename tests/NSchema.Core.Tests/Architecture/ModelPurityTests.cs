using NSchema.Model.Services;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static NSchema.Tests.Architecture.ArchitectureTestSupport;

namespace NSchema.Tests.Architecture;

/// <summary>
/// Guards the purity of the model namespaces (the <c>NSchema.Model</c> kernel and each slice's own): data shapes only (records, structs, enums,
/// interfaces, exceptions), depending on nothing but other model namespaces and the BCL.
/// </summary>
public sealed class ModelPurityTests
{
    // The kernel's models (excluding its services) and every stage's own .Domain.Models.
    private static readonly Regex ModelNamespace = new($@"{KernelModels}|\.Domain\.Models(\.|$)", RegexOptions.Compiled);

    [Fact]
    public void ModelNamespaces_ContainOnlyDataShapes()
    {
        // Arrange
        var types = CoreAssembly.GetTypes()
            .Where(t => t.Namespace is not null && ModelNamespace.IsMatch(t.Namespace))
            .Where(t => !t.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
            .ToList();

        // Act
        var offenders = types.Where(t => !IsDataShape(t)).Select(t => t.FullName).ToList();

        // Assert
        types.ShouldNotBeEmpty();
        offenders.ShouldBeEmpty($"Non-data types in model namespaces: {string.Join(", ", offenders)}");
    }

    // Every model namespace may read the kernel — that is what the kernel is for — so `allowed` lists only
    // what each may read *beyond* it. Project and State are the two sources and never reference each other;
    // a stage reads what is upstream of it.
    [Theory]
    [InlineData("NSchema.Project.Domain.Models", new[] { "NSchema.Project.Domain.Models", "System" })]
    [InlineData("NSchema.State.Domain.Models", new[] { "NSchema.State.Domain.Models", "System" })]
    [InlineData("NSchema.Diff.Domain.Models", new[] { "NSchema.Diff.Domain.Models", "System" })]
    [InlineData("NSchema.Plan.Domain.Models", new[] { "NSchema.Plan.Domain.Models", "NSchema.Diff.Domain.Models", "System" })]
    public void ModelNamespace_DependsOnlyOnTheKernelItselfAndBcl(string source, string[] allowed)
    {
        // Arrange
        var rule = Types().That().ResideInNamespaceMatching(Subtree(source))
            .Should().OnlyDependOn(
                Types().That().ResideInNamespaceMatching(Subtree(allowed))
                    .Or().ResideInNamespaceMatching(KernelModels));

        // Assert
        rule.ShouldBeSatisfied();
    }

    [Fact]
    public void KernelModel_DependsOnlyOnItselfTheBcl_AndTheHashingDomainService()
    {
        // Arrange — the kernel is the floor: it reads nothing but itself and the BCL, so nothing it carries
        // (an address, say) can presuppose a slice. The one sanctioned model → service edge is the script
        // models' canonical Hash properties delegating to ScriptHashing.
        var rule = Types().That().ResideInNamespaceMatching(KernelModels)
            .Should().OnlyDependOn(
                Types().That().ResideInNamespaceMatching(KernelModels)
                    .Or().ResideInNamespaceMatching(Subtree("System"))
                    .Or().Are(typeof(ScriptHashing)));

        // Assert
        rule.ShouldBeSatisfied();
    }

    // A data shape carries state, not behaviour: any struct or enum, an interface, an exception,
    // or a record class. Plain (non-record) classes — including static helpers — do not belong in
    // a model namespace.
    private static bool IsDataShape(Type type) =>
        type.IsValueType
        || type.IsInterface
        || typeof(Exception).IsAssignableFrom(type)
        || IsRecord(type);

    // Every record class declares its own EqualityContract override; nothing else does.
    private static bool IsRecord(Type type) =>
        type.GetProperty("EqualityContract", BindingFlags.Instance | BindingFlags.NonPublic) is not null;
}
