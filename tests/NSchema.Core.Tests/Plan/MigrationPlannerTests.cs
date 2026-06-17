using NSchema.Diff;
using NSchema.Diff.Model;
using NSchema.Plan;
using NSchema.Plan.Model;
using NSchema.Policies;
using NSchema.Schema;
using NSchema.Schema.Model;

namespace NSchema.Tests.Plan;

public sealed class MigrationPlannerTests
{
    private static readonly DatabaseSchema _emptySchema = new([]);
    private static readonly DatabaseDiff _emptyDiff = new([]);
    private static readonly IReadOnlyList<Script> _noScripts = [];

    private readonly ISchemaComparer _comparer = Substitute.For<ISchemaComparer>();
    private readonly IPlanLinearizer _linearizer = Substitute.For<IPlanLinearizer>();
    private readonly List<ISchemaPolicy> _schemaPolicies = [];
    private readonly List<IDiffPolicy> _diffPolicies = [];

    private MigrationPlanner Sut => new(_comparer, _linearizer, _schemaPolicies, _diffPolicies);

    public MigrationPlannerTests()
    {
        _comparer.Compare(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>()).Returns(_emptyDiff);
        _linearizer.Linearize(Arg.Any<DatabaseDiff>())
            .Returns(_ => []);
    }

    [Fact]
    public void Validate_RunsSchemaPoliciesAgainstDesiredSchema()
    {
        // Arrange
        var desired = new DatabaseSchema([new SchemaDefinition("app")]);
        var policy = Substitute.For<ISchemaPolicy>();
        policy.Validate(desired).Returns([PolicyDiagnostic.Error("Test", "bad schema")]);
        _schemaPolicies.Add(policy);

        // Act
        var diagnostics = Sut.Validate(desired);

        // Assert
        diagnostics.ShouldHaveSingleItem().Message.ShouldBe("bad schema");
        policy.Received(1).Validate(desired);
    }

    [Fact]
    public void Plan_SchemaPolicyError_ShortCircuitsBeforeDiffing()
    {
        // Arrange
        var policy = Substitute.For<ISchemaPolicy>();
        policy.Validate(Arg.Any<DatabaseSchema>()).Returns([PolicyDiagnostic.Error("Test", "bad schema")]);
        _schemaPolicies.Add(policy);

        // Act
        var result = Sut.Plan(_emptySchema, _emptySchema, _noScripts);

        // Assert: the schema stage is fatal — no diff, no plan.
        result.HasErrors.ShouldBeTrue();
        result.Plan.ShouldBeNull();
        result.Diff.ShouldBeNull();
        _comparer.DidNotReceive().Compare(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>());
    }

    [Fact]
    public void Plan_NonFatalSchemaDiagnostics_FlowIntoResult()
    {
        // Arrange
        var policy = Substitute.For<ISchemaPolicy>();
        policy.Validate(Arg.Any<DatabaseSchema>())
            .Returns([new PolicyDiagnostic("Test", "lint", PolicyDiagnosticSeverity.Warning)]);
        _schemaPolicies.Add(policy);

        // Act
        var result = Sut.Plan(_emptySchema, _emptySchema, _noScripts);

        // Assert: a non-error schema finding is carried through alongside the plan.
        result.HasErrors.ShouldBeFalse();
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldBe("lint");
    }

    [Fact]
    public void Plan_PassesBothSchemasToComparer()
    {
        // Arrange
        var current = new DatabaseSchema([new SchemaDefinition("current")]);
        var desired = new DatabaseSchema([new SchemaDefinition("desired")]);

        // Act
        Sut.Plan(current, desired, _noScripts);

        // Assert
        _comparer.Received(1).Compare(current, desired);
    }

    [Fact]
    public void Plan_AttachesPreAndPostDeploymentScriptsToPlan()
    {
        // Arrange
        var coreAction = new CreateSchema("app");
        _linearizer.Linearize(Arg.Any<DatabaseDiff>())
            .Returns(_ => [coreAction]);
        IReadOnlyList<Script> scripts =
        [
            new Script("pre", "SELECT 1", ScriptType.PreDeployment),
            new Script("post", "SELECT 2", ScriptType.PostDeployment),
        ];

        // Act
        var result = Sut.Plan(_emptySchema, _emptySchema, scripts);

        // Assert: scripts live on the plan (not interleaved into Actions, which carry only schema changes).
        result.Plan.ShouldNotBeNull();
        result.Plan.Actions.ShouldHaveSingleItem().ShouldBe(coreAction);
        result.Plan.PreDeploymentScripts.ShouldBe([scripts[0]]);
        result.Plan.PostDeploymentScripts.ShouldBe([scripts[1]]);
    }

    [Fact]
    public void Plan_NoScripts_DoesNotAlterPlan()
    {
        // Arrange
        var coreAction = new CreateSchema("app");
        _linearizer.Linearize(Arg.Any<DatabaseDiff>())
            .Returns([coreAction]);

        // Act
        var result = Sut.Plan(_emptySchema, _emptySchema, _noScripts);

        // Assert
        result.Plan.ShouldNotBeNull();
        result.Plan.Actions.ShouldHaveSingleItem();
        result.Plan.Actions[0].ShouldBe(coreAction);
    }

    [Fact]
    public void Plan_RunsDiffPoliciesAgainstTheDiff()
    {
        // Arrange
        var policy = Substitute.For<IDiffPolicy>();
        policy.Validate(_emptyDiff).Returns([PolicyDiagnostic.Error("Test", "destructive")]);
        _diffPolicies.Add(policy);

        // Act
        var result = Sut.Plan(_emptySchema, _emptySchema, _noScripts);

        // Assert
        result.Diagnostics.ShouldHaveSingleItem();
        result.Diagnostics[0].Message.ShouldBe("destructive");
        policy.Received(1).Validate(_emptyDiff);
    }

    [Fact]
    public void PlanTeardown_DiffsManagedSchemaAgainstEmpty()
    {
        // Arrange
        var managed = new DatabaseSchema([new SchemaDefinition("app")]);

        // Act
        Sut.PlanTeardown(managed);

        // Assert: the managed schema is diffed against an empty desired schema.
        _comparer.Received(1).Compare(managed, Arg.Is<DatabaseSchema>(d => d.Schemas.Count == 0 && d.DroppedSchemas.Count == 0));
    }

    [Fact]
    public void PlanTeardown_LinearizesTheDiff_WithoutDiagnostics()
    {
        // Arrange
        List<MigrationAction> actions = [new DropSchema("app")];
        _linearizer.Linearize(_emptyDiff).Returns(actions);

        // Act
        var result = Sut.PlanTeardown(new DatabaseSchema([new SchemaDefinition("app")]));

        // Assert
        result.Plan!.Actions.ShouldBe(actions);
        result.Diff.ShouldBe(_emptyDiff);
        result.HasErrors.ShouldBeFalse();
        result.Diagnostics.Count.ShouldBe(0);
    }

    [Fact]
    public void PlanTeardown_BypassesPolicies()
    {
        // Arrange
        var diffPolicy = Substitute.For<IDiffPolicy>();

        _diffPolicies.Add(diffPolicy);

        // Act
        Sut.PlanTeardown(new DatabaseSchema([new SchemaDefinition("app")]));

        // Assert
        diffPolicy.DidNotReceive().Validate(Arg.Any<DatabaseDiff>());
    }
}
