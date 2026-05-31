using NSchema.Migration;
using NSchema.Migration.Plan;
using NSchema.Policies;
using NSchema.Schema;

namespace NSchema.Tests.Migration;

public sealed class DefaultMigrationPlannerTests
{
    private static readonly DatabaseSchema EmptySchema = DatabaseSchema.Create([]);

    private readonly List<IScriptProvider> _scripts = [];
    private readonly ISchemaComparer _comparer = Substitute.For<ISchemaComparer>();
    private readonly List<ISchemaPolicy> _schemaPolicies = [];
    private readonly List<IMigrationPlanTransformer> _transformers = [];
    private readonly List<IMigrationPolicy> _migrationPolicies = [];

    private DefaultMigrationPlanner _sut => new(
        _scripts,
        _comparer,
        _schemaPolicies,
        _transformers,
        _migrationPolicies
    );

    public DefaultMigrationPlannerTests()
    {
        _comparer.Compare(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>())
            .Returns(call => new MigrationPlan([], call.ArgAt<DatabaseSchema>(1)));
    }

    [Fact]
    public async Task Plan_RunsSchemaPoliciesAndReturnsErrorDiagnostics()
    {
        // Arrange
        var policy = Substitute.For<ISchemaPolicy>();
        policy.Validate(Arg.Any<DatabaseSchema>()).Returns([new PolicyError("Test", "broken")]);
        _schemaPolicies.Add(policy);

        // Act
        var result = await _sut.Plan(EmptySchema, EmptySchema);

        // Assert
        result.Plan.ShouldBeNull();
        result.Diagnostics.ShouldHaveSingleItem();
        result.Diagnostics[0].Message.ShouldBe("broken");
        result.Diagnostics[0].Severity.ShouldBe(PolicySeverity.Error);
    }

    [Fact]
    public async Task Plan_SchemaPolicyFailure_SkipsComparisonEntirely()
    {
        // Arrange
        var policy = Substitute.For<ISchemaPolicy>();
        policy.Validate(Arg.Any<DatabaseSchema>()).Returns([new PolicyError("Test", "fail")]);
        _schemaPolicies.Add(policy);

        // Act
        await _sut.Plan(EmptySchema, EmptySchema);

        // Assert
        _comparer.DidNotReceive().Compare(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>());
    }

    [Fact]
    public async Task Plan_PassesBothSchemasToComparer()
    {
        // Arrange
        var current = DatabaseSchema.Create([SchemaDefinition.Create("current")]);
        var desired = DatabaseSchema.Create([SchemaDefinition.Create("desired")]);

        // Act
        await _sut.Plan(current, desired);

        // Assert
        _comparer.Received(1).Compare(current, desired);
    }

    [Fact]
    public async Task Plan_InjectsPreAndPostDeploymentScriptsAroundActions()
    {
        // Arrange
        var coreAction = new CreateSchema("app");
        _comparer.Compare(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>())
            .Returns(call => new MigrationPlan([coreAction], call.ArgAt<DatabaseSchema>(1)));
        var scripts = Substitute.For<IScriptProvider>();
        scripts.GetScripts(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Script>>([
                new Script("pre", "SELECT 1", ScriptType.PreDeployment),
                new Script("post", "SELECT 2", ScriptType.PostDeployment),
            ]));
        _scripts.Add(scripts);

        // Act
        var result = await _sut.Plan(EmptySchema, EmptySchema);

        // Assert
        result.Plan.ShouldNotBeNull();
        result.Plan.Actions.Count.ShouldBe(3);
        result.Plan.Actions[0].ShouldBeOfType<RunScript>().Script.Name.ShouldBe("pre");
        result.Plan.Actions[1].ShouldBe(coreAction);
        result.Plan.Actions[2].ShouldBeOfType<RunScript>().Script.Name.ShouldBe("post");
    }

    [Fact]
    public async Task Plan_NoScriptProviders_DoesNotAlterPlan()
    {
        // Arrange
        var coreAction = new CreateSchema("app");
        _comparer.Compare(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>())
            .Returns(call => new MigrationPlan([coreAction], call.ArgAt<DatabaseSchema>(1)));

        // Act
        var result = await _sut.Plan(EmptySchema, EmptySchema);

        // Assert
        result.Plan.ShouldNotBeNull();
        result.Plan.Actions.ShouldHaveSingleItem();
        result.Plan.Actions[0].ShouldBe(coreAction);
    }

    [Fact]
    public async Task Plan_AppliesTransformersInRegistrationOrder()
    {
        // Arrange
        var t1 = Substitute.For<IMigrationPlanTransformer>();
        var t2 = Substitute.For<IMigrationPlanTransformer>();
        var after1 = new MigrationPlan([new CreateSchema("after1")], EmptySchema);
        var after2 = new MigrationPlan([new CreateSchema("after2")], EmptySchema);
        t1.Transform(Arg.Any<MigrationPlan>()).Returns(after1);
        t2.Transform(after1).Returns(after2);
        _transformers.Add(t1);
        _transformers.Add(t2);

        // Act
        var result = await _sut.Plan(EmptySchema, EmptySchema);

        // Assert
        result.Plan.ShouldBe(after2);
        Received.InOrder(() =>
        {
            t1.Transform(Arg.Any<MigrationPlan>());
            t2.Transform(after1);
        });
    }

    [Fact]
    public async Task Plan_RunsMigrationPoliciesAgainstTransformedPlan()
    {
        // Arrange
        var transformer = Substitute.For<IMigrationPlanTransformer>();
        var transformed = new MigrationPlan([new DropTable("app", "users")], EmptySchema);
        transformer.Transform(Arg.Any<MigrationPlan>()).Returns(transformed);
        _transformers.Add(transformer);
        var policy = Substitute.For<IMigrationPolicy>();
        policy.Validate(transformed).Returns([new PolicyError("Test", "destructive")]);
        _migrationPolicies.Add(policy);

        // Act
        var result = await _sut.Plan(EmptySchema, EmptySchema);

        // Assert
        result.Diagnostics.ShouldHaveSingleItem();
        result.Diagnostics[0].Message.ShouldBe("destructive");
        policy.Received(1).Validate(transformed);
    }

    [Fact]
    public async Task Plan_AggregatesDiagnosticsFromMultiplePolicies()
    {
        // Arrange
        var p1 = Substitute.For<ISchemaPolicy>();
        var p2 = Substitute.For<ISchemaPolicy>();
        p1.Validate(Arg.Any<DatabaseSchema>()).Returns([new PolicyError("P1", "a")]);
        p2.Validate(Arg.Any<DatabaseSchema>()).Returns([new PolicyError("P2", "b"), new PolicyError("P2", "c")]);
        _schemaPolicies.Add(p1);
        _schemaPolicies.Add(p2);

        // Act
        var result = await _sut.Plan(EmptySchema, EmptySchema);

        // Assert
        result.Diagnostics.Count.ShouldBe(3);
    }
}
