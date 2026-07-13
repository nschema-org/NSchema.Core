using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NSchema.Project.Domain;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static NSchema.Tests.Architecture.ArchitectureTestSupport;

namespace NSchema.Tests.Architecture;

/// <summary>
/// Guards the purity of the <c>*.Model</c> namespaces: data shapes only (records, structs, enums,
/// interfaces, exceptions), depending on nothing but other model namespaces and the BCL.
/// </summary>
public sealed class ModelPurityTests
{
    private static readonly Regex ModelNamespace = new(@"\.Model(s)?(\.|$)", RegexOptions.Compiled);

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

    [Theory]
    [InlineData("NSchema.Diff.Domain.Models", new[] { "NSchema.Diff.Domain.Models", "NSchema.Project.Domain.Models", "System" })]
    [InlineData("NSchema.Plan.Domain.Models", new[] { "NSchema.Plan.Domain.Models", "NSchema.Project.Domain.Models", "NSchema.Diff.Domain.Models", "System" })]
    [InlineData("NSchema.Current.Domain.Models", new[] { "NSchema.Current.Domain.Models", "NSchema.Project.Domain.Models", "System" })]
    public void ModelNamespace_DependsOnlyOnModelsAndBcl(string source, string[] allowed)
    {
        // Arrange
        var rule = Types().That().ResideInNamespaceMatching(Subtree(source))
            .Should().OnlyDependOn(Types().That().ResideInNamespaceMatching(Subtree(allowed)));

        // Assert
        rule.ShouldBeSatisfied();
    }

    [Fact]
    public void DdlNamespace_DependsOnlyOnModelsTheBcl_AndTheOutcomeGrammar()
    {
        // Arrange — the read seam returns Result, so the language lane may also reference the root
        // outcome grammar (Result/Diagnostic), but nothing else outside its lane. TRANSITIONAL: the old
        // lane's seam delegates to the Nsql pipeline (parser + projector) until NsqlReader replaces it
        // at the reader realignment; the sanctioned edge dies with DdlReader.
        var rule = Types().That().ResideInNamespaceMatching(Subtree("NSchema.Project.Ddl"))
            .Should().OnlyDependOn(
                Types().That().ResideInNamespaceMatching(Subtree("NSchema.Project.Ddl", "NSchema.Project.Domain.Models", "NSchema.Plugins", "System"))
                    .Or().Are(typeof(Result), typeof(Result<>), typeof(Diagnostic),
                        typeof(NSchema.Project.Nsql.NsqlParser), typeof(NSchema.Project.Nsql.DocumentProjector), typeof(NSchema.Project.Nsql.NsqlDocument)));

        // Assert
        rule.ShouldBeSatisfied();
    }

    [Fact]
    public void SchemaModel_DependsOnlyOnItselfTheBcl_AndTheHashingDomainService()
    {
        // Arrange — the one sanctioned model → domain-service edge: the script models' canonical
        // Hash properties delegate to ScriptHashing at the Schema domain root.
        var rule = Types().That().ResideInNamespaceMatching(Subtree("NSchema.Project.Domain.Models"))
            .Should().OnlyDependOn(
                Types().That().ResideInNamespaceMatching(Subtree("NSchema.Project.Domain.Models", "System"))
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
