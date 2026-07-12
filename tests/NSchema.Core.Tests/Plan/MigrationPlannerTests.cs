using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Models;
using NSchema.Diff.Policies;
using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Models.Schemas;
using NSchema.Plan.Domain.Models.Scripts;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Schemas;
using NSchema.Project.Domain.Models.Scripts;
using NSchema.Project.Policies;
using NSchema.Tests.Helpers;

namespace NSchema.Tests.Plan;

/// <summary>
/// The planner conducts the pipeline: it validates each stage's output and mechanically realizes the complete
/// diff as SQL. The diff-stage intelligence itself is covered by <see cref="Tests.Diff.ProjectComparerTests"/>.
/// </summary>
public sealed class MigrationPlannerTests
{
    private static readonly DatabaseSchema _emptySchema = new([]);
    private static readonly DatabaseDiff _emptyDiff = new([]);
    private static readonly CurrentState _current = new(_emptySchema);
    private static readonly ProjectDefinition _desired = new(_emptySchema, []);

    private readonly IProjectComparer _differ = Substitute.For<IProjectComparer>();
    private readonly IPlanLinearizer _linearizer = Substitute.For<IPlanLinearizer>();
    private readonly List<ISchemaPolicy> _schemaPolicies = [];
    private readonly List<IDiffPolicy> _diffPolicies = [];

    private MigrationPlanner Sut => new(_differ, _linearizer, _schemaPolicies, _diffPolicies, new StubSqlDialect());

    public MigrationPlannerTests()
    {
        _differ.Compare(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>()).Returns(Result.From(_emptyDiff, []));
        _differ.CompareTeardown(Arg.Any<DatabaseSchema>()).Returns(_emptyDiff);
        _linearizer.Linearize(Arg.Any<DatabaseDiff>()).Returns(_ => []);
    }

    [Fact]
    public void Validate_RunsSchemaPoliciesAgainstDesiredSchema()
    {
        // Arrange
        var desired = new DatabaseSchema([new SchemaDefinition("app")]);
        var policy = Substitute.For<ISchemaPolicy>();
        policy.Validate(desired).Returns([Diagnostic.Error("Test", "bad schema")]);
        _schemaPolicies.Add(policy);

        // Act
        var diagnostics = Sut.Validate(desired);

        // Assert
        diagnostics.ShouldHaveSingleItem().Message.ShouldBe("bad schema");
        policy.Received(1).Validate(desired);
    }

    [Fact]
    public void Plan_SchemaPolicyError_BlocksButStillCarriesTheCompletePlan()
    {
        // Arrange
        var policy = Substitute.For<ISchemaPolicy>();
        policy.Validate(Arg.Any<DatabaseSchema>()).Returns([Diagnostic.Error("Test", "bad schema")]);
        _schemaPolicies.Add(policy);

        // Act
        var result = Sut.Plan(_current, _desired);

        // Assert: a policy block means "may not apply", not "stopped computing" — the failure carries the
        // complete plan so the offending change stays visible.
        result.IsFailure.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        _differ.Received(1).Compare(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>());
    }

    [Fact]
    public void Plan_NonFatalSchemaDiagnostics_FlowIntoResult()
    {
        // Arrange
        var policy = Substitute.For<ISchemaPolicy>();
        policy.Validate(Arg.Any<DatabaseSchema>())
            .Returns([new Diagnostic("Test", "lint", DiagnosticSeverity.Warning)]);
        _schemaPolicies.Add(policy);

        // Act
        var result = Sut.Plan(_current, _desired);

        // Assert: a non-error schema finding is carried through alongside the plan.
        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldBe("lint");
    }

    [Fact]
    public void Plan_PassesCurrentAndDesiredToTheDiffer()
    {
        // Act
        Sut.Plan(_current, _desired);

        // Assert
        _differ.Received(1).Compare(_current, _desired);
    }

    [Fact]
    public void Plan_MergesTheDifferDiagnosticsIntoTheResult()
    {
        // Arrange — run-once skips and dead-migration findings are diff-stage diagnostics; the planner surfaces them.
        _differ.Compare(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>())
            .Returns(Result.From(_emptyDiff, [Diagnostic.Info("data-migrations", "inert block")]));

        // Act
        var result = Sut.Plan(_current, _desired);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldBe("inert block");
    }

    [Fact]
    public void Plan_RunsDiffPoliciesAgainstTheCompleteDiff()
    {
        // Arrange
        var diff = _emptyDiff with { Scripts = [new Script("seed", "SELECT 1", new DeploymentEvent(DeploymentPhase.Post))] };
        _differ.Compare(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>()).Returns(Result.From(diff, []));
        var policy = Substitute.For<IDiffPolicy>();
        policy.Validate(diff).Returns([Diagnostic.Error("Test", "destructive")]);
        _diffPolicies.Add(policy);

        // Act
        var result = Sut.Plan(_current, _desired);

        // Assert — the policy received the diff the differ produced, scripts included.
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldBe("destructive");
        policy.Received(1).Validate(diff);
    }

    [Fact]
    public void Plan_RendersEveryActionThroughTheDialect_ScriptsIncluded()
    {
        // Arrange — the linearizer's ordered actions render one by one through the dialect; the stub renders
        // an ExecuteScript as its verbatim Statement, carrying the transaction placement.
        var script = new Script("seed", "INSERT INTO app.c VALUES (1);", new DeploymentEvent(DeploymentPhase.Post)) { RunOutsideTransaction = true };
        _linearizer.Linearize(Arg.Any<DatabaseDiff>())
            .Returns(_ => [new CreateSchema("app"), new ExecuteScript(script)]);

        // Act
        var result = Sut.Plan(_current, _desired);

        // Assert
        result.Value!.Statements.Select(s => s.Sql).ShouldBe([$"-- {nameof(CreateSchema)}", script.Sql]);
        result.Value!.Statements[1].RunOutsideTransaction.ShouldBeTrue();
    }

    [Fact]
    public void Plan_CarriesTheDifferDiffOnTheArtifact()
    {
        // Arrange
        var diff = _emptyDiff with { Scripts = [new Script("seed", "SELECT 1", new DeploymentEvent(DeploymentPhase.Post))] };
        _differ.Compare(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>()).Returns(Result.From(diff, []));

        // Act
        var result = Sut.Plan(_current, _desired);

        // Assert
        result.Value!.Diff.ShouldBe(diff);
    }

    [Fact]
    public void Plan_WithoutADialect_Fails()
    {
        // Arrange
        var sut = new MigrationPlanner(_differ, _linearizer, _schemaPolicies, _diffPolicies, dialect: null);

        // Act
        var result = sut.Plan(_current, _desired);

        // Assert — a dialect is required: there is no SQL-less plan.
        result.IsFailure.ShouldBeTrue();
        result.Value.ShouldBeNull();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("requires a database provider");
    }

    [Fact]
    public void PlanTeardown_RealizesTheTeardownDiff_WithoutDiagnostics()
    {
        // Arrange
        _linearizer.Linearize(_emptyDiff).Returns([new DropSchema("app")]);

        // Act
        var result = Sut.PlanTeardown(new DatabaseSchema([new SchemaDefinition("app")]));

        // Assert
        _differ.Received(1).CompareTeardown(Arg.Any<DatabaseSchema>());
        result.Value!.Statements.ShouldHaveSingleItem().Sql.ShouldBe($"-- {nameof(DropSchema)}");
        result.Value!.Diff.ShouldBe(_emptyDiff);
        result.IsSuccess.ShouldBeTrue();
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

    [Fact]
    public void PlanTeardown_WithoutADialect_Fails()
    {
        // Arrange
        var sut = new MigrationPlanner(_differ, _linearizer, _schemaPolicies, _diffPolicies, dialect: null);

        // Act
        var result = sut.PlanTeardown(_emptySchema);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("requires a database provider");
    }
}
