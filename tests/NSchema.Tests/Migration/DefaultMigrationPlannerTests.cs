using NSchema.Diff;
using NSchema.Diff.Model;
using NSchema.Migration;
using NSchema.Plan;
using NSchema.Plan.Model;
using NSchema.Policies;
using NSchema.Schema;
using NSchema.Schema.Model;

namespace NSchema.Tests.Migration;

public sealed class DefaultMigrationPlannerTests
{
    private static readonly DatabaseSchema _emptySchema = DatabaseSchema.Create([]);
    private static readonly MigrationDiff _emptyDiff = new([], [], []);
    private static readonly IReadOnlyList<Script> _noScripts = [];

    private readonly ISchemaComparer _comparer = Substitute.For<ISchemaComparer>();
    private readonly IMigrationLinearizer _linearizer = Substitute.For<IMigrationLinearizer>();
    private readonly List<ISchemaPolicy> _schemaPolicies = [];
    private readonly List<IDiffTransformer> _diffTransformers = [];
    private readonly List<IDiffPolicy> _diffPolicies = [];
    private readonly List<IMigrationPlanTransformer> _transformers = [];
    private readonly List<IMigrationPolicy> _migrationPolicies = [];

    private DefaultMigrationPlanner Sut => new(
        _comparer,
        _linearizer,
        _schemaPolicies,
        _diffTransformers,
        _diffPolicies,
        _transformers,
        _migrationPolicies
    );

    public DefaultMigrationPlannerTests()
    {
        _comparer.Compare(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>()).Returns(_emptyDiff);
        _linearizer.Linearize(Arg.Any<MigrationDiff>(), Arg.Any<DatabaseSchema>())
            .Returns(call => new MigrationPlan([], call.ArgAt<DatabaseSchema>(1)));
    }

    [Fact]
    public void Plan_RunsSchemaPoliciesAndReturnsErrorDiagnostics()
    {
        // Arrange
        var policy = Substitute.For<ISchemaPolicy>();
        policy.Validate(Arg.Any<DatabaseSchema>()).Returns([new PolicyError("Test", "broken")]);
        _schemaPolicies.Add(policy);

        // Act
        var result = Sut.Plan(_emptySchema, _emptySchema, _noScripts);

        // Assert
        result.Plan.ShouldBeNull();
        result.Diff.ShouldBeNull();
        result.Diagnostics.ShouldHaveSingleItem();
        result.Diagnostics[0].Message.ShouldBe("broken");
        result.Diagnostics[0].Severity.ShouldBe(PolicySeverity.Error);
    }

    [Fact]
    public void Plan_SchemaPolicyFailure_SkipsComparisonEntirely()
    {
        // Arrange
        var policy = Substitute.For<ISchemaPolicy>();
        policy.Validate(Arg.Any<DatabaseSchema>()).Returns([new PolicyError("Test", "fail")]);
        _schemaPolicies.Add(policy);

        // Act
        Sut.Plan(_emptySchema, _emptySchema, _noScripts);

        // Assert
        _comparer.DidNotReceive().Compare(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>());
    }

    [Fact]
    public void Plan_PassesBothSchemasToComparer()
    {
        // Arrange
        var current = DatabaseSchema.Create([SchemaDefinition.Create("current")]);
        var desired = DatabaseSchema.Create([SchemaDefinition.Create("desired")]);

        // Act
        Sut.Plan(current, desired, _noScripts);

        // Assert
        _comparer.Received(1).Compare(current, desired);
    }

    [Fact]
    public void Plan_InjectsPreAndPostDeploymentScriptsAroundActions()
    {
        // Arrange
        var coreAction = new CreateSchema("app");
        _linearizer.Linearize(Arg.Any<MigrationDiff>(), Arg.Any<DatabaseSchema>())
            .Returns(call => new MigrationPlan([coreAction], call.ArgAt<DatabaseSchema>(1)));
        IReadOnlyList<Script> scripts =
        [
            new Script("pre", "SELECT 1", ScriptType.PreDeployment),
            new Script("post", "SELECT 2", ScriptType.PostDeployment),
        ];

        // Act
        var result = Sut.Plan(_emptySchema, _emptySchema, scripts);

        // Assert
        result.Plan.ShouldNotBeNull();
        result.Plan.Actions.Count.ShouldBe(3);
        result.Plan.Actions[0].ShouldBeOfType<RunScript>().Script.Name.ShouldBe("pre");
        result.Plan.Actions[1].ShouldBe(coreAction);
        result.Plan.Actions[2].ShouldBeOfType<RunScript>().Script.Name.ShouldBe("post");
    }

    [Fact]
    public void Plan_ScriptNamesAreCarriedOntoTheDiff()
    {
        // Arrange
        IReadOnlyList<Script> scripts =
        [
            new Script("pre", "SELECT 1", ScriptType.PreDeployment),
            new Script("post", "SELECT 2", ScriptType.PostDeployment),
        ];

        // Act
        var result = Sut.Plan(_emptySchema, _emptySchema, scripts);

        // Assert
        result.Diff.ShouldNotBeNull();
        result.Diff.PreDeploymentScripts.ShouldBe(["pre"]);
        result.Diff.PostDeploymentScripts.ShouldBe(["post"]);
    }

    [Fact]
    public void Plan_NoScripts_DoesNotAlterPlan()
    {
        // Arrange
        var coreAction = new CreateSchema("app");
        _linearizer.Linearize(Arg.Any<MigrationDiff>(), Arg.Any<DatabaseSchema>())
            .Returns(call => new MigrationPlan([coreAction], call.ArgAt<DatabaseSchema>(1)));

        // Act
        var result = Sut.Plan(_emptySchema, _emptySchema, _noScripts);

        // Assert
        result.Plan.ShouldNotBeNull();
        result.Plan.Actions.ShouldHaveSingleItem();
        result.Plan.Actions[0].ShouldBe(coreAction);
    }

    [Fact]
    public void Plan_AppliesDiffTransformersBeforeLinearizing()
    {
        // Arrange
        var transformed = new MigrationDiff([new SchemaDiff("app", ChangeKind.Add, null, null, [], [])], [], []);
        var transformer = Substitute.For<IDiffTransformer>();
        transformer.Transform(_emptyDiff).Returns(transformed);
        _diffTransformers.Add(transformer);

        // Act
        Sut.Plan(_emptySchema, _emptySchema, _noScripts);

        // Assert
        _linearizer.Received(1).Linearize(transformed, Arg.Any<DatabaseSchema>());
    }

    [Fact]
    public void Plan_RunsDiffPoliciesAgainstTheDiff()
    {
        // Arrange
        var policy = Substitute.For<IDiffPolicy>();
        policy.Validate(_emptyDiff).Returns([new PolicyError("Test", "destructive")]);
        _diffPolicies.Add(policy);

        // Act
        var result = Sut.Plan(_emptySchema, _emptySchema, _noScripts);

        // Assert
        result.Diagnostics.ShouldHaveSingleItem();
        result.Diagnostics[0].Message.ShouldBe("destructive");
        policy.Received(1).Validate(_emptyDiff);
    }

    [Fact]
    public void Plan_AppliesTransformersInRegistrationOrder()
    {
        // Arrange
        var t1 = Substitute.For<IMigrationPlanTransformer>();
        var t2 = Substitute.For<IMigrationPlanTransformer>();
        var after1 = new MigrationPlan([new CreateSchema("after1")], _emptySchema);
        var after2 = new MigrationPlan([new CreateSchema("after2")], _emptySchema);
        t1.Transform(Arg.Any<MigrationPlan>()).Returns(after1);
        t2.Transform(after1).Returns(after2);
        _transformers.Add(t1);
        _transformers.Add(t2);

        // Act
        var result = Sut.Plan(_emptySchema, _emptySchema, _noScripts);

        // Assert
        result.Plan.ShouldBe(after2);
        Received.InOrder(() =>
        {
            t1.Transform(Arg.Any<MigrationPlan>());
            t2.Transform(after1);
        });
    }

    [Fact]
    public void Plan_RunsMigrationPoliciesAgainstTransformedPlan()
    {
        // Arrange
        var transformer = Substitute.For<IMigrationPlanTransformer>();
        var transformed = new MigrationPlan([new DropTable("app", "users")], _emptySchema);
        transformer.Transform(Arg.Any<MigrationPlan>()).Returns(transformed);
        _transformers.Add(transformer);
        var policy = Substitute.For<IMigrationPolicy>();
        policy.Validate(transformed).Returns([new PolicyError("Test", "destructive")]);
        _migrationPolicies.Add(policy);

        // Act
        var result = Sut.Plan(_emptySchema, _emptySchema, _noScripts);

        // Assert
        result.Diagnostics.ShouldHaveSingleItem();
        result.Diagnostics[0].Message.ShouldBe("destructive");
        policy.Received(1).Validate(transformed);
    }

    [Fact]
    public void Plan_AggregatesDiagnosticsFromMultiplePolicies()
    {
        // Arrange
        var p1 = Substitute.For<ISchemaPolicy>();
        var p2 = Substitute.For<ISchemaPolicy>();
        p1.Validate(Arg.Any<DatabaseSchema>()).Returns([new PolicyError("P1", "a")]);
        p2.Validate(Arg.Any<DatabaseSchema>()).Returns([new PolicyError("P2", "b"), new PolicyError("P2", "c")]);
        _schemaPolicies.Add(p1);
        _schemaPolicies.Add(p2);

        // Act
        var result = Sut.Plan(_emptySchema, _emptySchema, _noScripts);

        // Assert
        result.Diagnostics.Count.ShouldBe(3);
    }
}
