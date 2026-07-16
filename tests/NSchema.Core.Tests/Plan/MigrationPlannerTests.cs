using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Models;
using NSchema.Diff.Domain.Models.Schemas;
using NSchema.Model;
using NSchema.Model.Schemas;
using NSchema.Model.Scripts;
using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Models;
using NSchema.Plan.Domain.Models.Schemas;
using NSchema.Plan.Domain.Models.Scripts;
using NSchema.Plan.Policies;
using NSchema.Project.Domain.Models;
using NSchema.Project.Policies;

namespace NSchema.Tests.Plan;

/// <summary>
/// The planner conducts the pipeline: it validates each stage's output and mechanically realizes the complete
/// diff as SQL. The diff-stage intelligence itself is covered by <see cref="Tests.Diff.ProjectComparerTests"/>.
/// </summary>
public sealed class MigrationPlannerTests
{
    private static readonly Database _emptySchema = new([]);
    private static readonly DatabaseDiff _emptyDiff = new([]);
    private static readonly CurrentState _current = new(_emptySchema);
    private static readonly ProjectDefinition _desired = new(_emptySchema);

    private readonly IProjectComparer _differ = Substitute.For<IProjectComparer>();
    private readonly IPlanLinearizer _linearizer = Substitute.For<IPlanLinearizer>();
    private readonly List<IProjectPolicy> _projectPolicies = [];
    private readonly List<IPlanPolicy> _planPolicies = [];

    private MigrationPlanner Sut => new(_differ, _linearizer, _projectPolicies, _planPolicies, new StubSqlDialect());

    public MigrationPlannerTests()
    {
        _differ.Compare(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>()).Returns(Result.From(_emptyDiff, []));
        _linearizer.Linearize(Arg.Any<DatabaseDiff>()).Returns(_ => []);
    }

    /// <summary>A difference touching two schemas, so a scope has something to narrow away.</summary>
    private static DatabaseDiff TwoSchemaDiff() => new(
    [
        new SchemaDiff(new SqlIdentifier("app"), ChangeKind.Remove),
        new SchemaDiff(new SqlIdentifier("billing"), ChangeKind.Remove),
    ]);

    [Fact]
    public void Plan_ComparesTheWholeStates_ThenNarrowsTheDifferenceToTheScope()
    {
        // Arrange — the comparer answers what differs between two whole states, a complete question that
        // needs no scope; the planner applies the scope to its answer.
        _differ.Compare(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>()).Returns(Result.From(TwoSchemaDiff(), []));

        // Act
        var result = Sut.Plan(_current, _desired, PlanningScope.Of(new SqlIdentifier("app")));

        // Assert
        result.Value!.Diff.Schemas.ShouldHaveSingleItem().Name.ShouldBe("app");
    }

    [Fact]
    public void Plan_RunsPlanPoliciesAgainstTheNarrowedDiff_NotTheWholeComparison()
    {
        // Arrange — comparing whole states manufactures a removal for every out-of-scope schema. If policies
        // saw those, an ordinary scoped plan would report changes it is never going to make.
        _differ.Compare(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>()).Returns(Result.From(TwoSchemaDiff(), []));
        var policy = Substitute.For<IPlanPolicy>();
        _planPolicies.Add(policy);

        // Act
        Sut.Plan(_current, _desired, PlanningScope.Of(new SqlIdentifier("app")));

        // Assert
        policy.Received(1).Validate(Arg.Is<MigrationPlan>(p => p!.Diff.Schemas.Count == 1
            && p.Diff.Schemas[0].Name == new SqlIdentifier("app")));
    }

    [Fact]
    public void Plan_LinearizesOnlyTheNarrowedDiff()
    {
        // Arrange — the SQL must not contain out-of-scope work either.
        _differ.Compare(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>()).Returns(Result.From(TwoSchemaDiff(), []));

        // Act
        Sut.Plan(_current, _desired, PlanningScope.Of(new SqlIdentifier("app")));

        // Assert
        _linearizer.Received(1).Linearize(Arg.Is<DatabaseDiff>(d => d!.Schemas.Count == 1));
    }

    [Fact]
    public void Validate_RunsProjectPoliciesAgainstTheProject()
    {
        // Arrange
        var desired = new ProjectDefinition(new Database([new Schema(new SqlIdentifier("app"))]));
        var policy = Substitute.For<IProjectPolicy>();
        policy.Validate(desired).Returns([Diagnostic.Error("Test", "bad schema")]);
        _projectPolicies.Add(policy);

        // Act
        var diagnostics = Sut.Validate(desired);

        // Assert
        diagnostics.Diagnostics.ShouldHaveSingleItem().Message.ShouldBe("bad schema");
        policy.Received(1).Validate(desired);
    }

    [Fact]
    public void Plan_SchemaPolicyError_BlocksButStillCarriesTheCompletePlan()
    {
        // Arrange
        var policy = Substitute.For<IProjectPolicy>();
        policy.Validate(Arg.Any<ProjectDefinition>()).Returns([Diagnostic.Error("Test", "bad schema")]);
        _projectPolicies.Add(policy);

        // Act
        var result = Sut.Plan(_current, _desired, PlanningScope.All);

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
        var policy = Substitute.For<IProjectPolicy>();
        policy.Validate(Arg.Any<ProjectDefinition>())
            .Returns([new Diagnostic("Test", "lint", DiagnosticSeverity.Warning)]);
        _projectPolicies.Add(policy);

        // Act
        var result = Sut.Plan(_current, _desired, PlanningScope.All);

        // Assert: a non-error schema finding is carried through alongside the plan.
        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldBe("lint");
    }

    [Fact]
    public void Plan_PassesCurrentAndDesiredToTheDiffer()
    {
        // Act
        Sut.Plan(_current, _desired, PlanningScope.All);

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
        var result = Sut.Plan(_current, _desired, PlanningScope.All);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldBe("inert block");
    }

    [Fact]
    public void Plan_RunsPlanPoliciesAgainstTheCompletePlan()
    {
        // Arrange
        var diff = _emptyDiff with { DeploymentScripts = [new DeploymentScript(new SqlIdentifier("seed"), new SqlText("SELECT 1"), null, DeploymentPhase.Post)] };
        _differ.Compare(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>()).Returns(Result.From(diff, []));
        var policy = Substitute.For<IPlanPolicy>();
        policy.Validate(Arg.Is<MigrationPlan>(p => p!.Diff == diff)).Returns([Diagnostic.Error("Test", "destructive")]);
        _planPolicies.Add(policy);

        // Act
        var result = Sut.Plan(_current, _desired, PlanningScope.All);

        // Assert — the policy received the rendered plan carrying the diff the differ produced, scripts included.
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldBe("destructive");
        policy.Received(1).Validate(Arg.Is<MigrationPlan>(p => p!.Diff == diff));
    }

    [Fact]
    public void Plan_RendersEveryActionThroughTheDialect_ScriptsIncluded()
    {
        // Arrange — the linearizer's ordered actions render one by one through the dialect; the stub renders
        // an ExecuteScript as its verbatim Statement, carrying the transaction placement.
        var script = new DeploymentScript(new SqlIdentifier("seed"), new SqlText("INSERT INTO app.c VALUES (1);"), null, DeploymentPhase.Post) { RunOutsideTransaction = true };
        _linearizer.Linearize(Arg.Any<DatabaseDiff>())
            .Returns(_ => [new CreateSchema(new SqlIdentifier("app")), new ExecuteScript(script)]);

        // Act
        var result = Sut.Plan(_current, _desired, PlanningScope.All);

        // Assert
        result.Value!.Statements.Select(s => s.Sql).ShouldBe([$"-- {nameof(CreateSchema)}", script.Sql.Value]);
        result.Value!.Statements[1].RunOutsideTransaction.ShouldBeTrue();
    }

    [Fact]
    public void Plan_CarriesTheDifferDiffOnTheArtifact()
    {
        // Arrange
        var diff = _emptyDiff with { DeploymentScripts = [new DeploymentScript(new SqlIdentifier("seed"), new SqlText("SELECT 1"), null, DeploymentPhase.Post)] };
        _differ.Compare(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>()).Returns(Result.From(diff, []));

        // Act
        var result = Sut.Plan(_current, _desired, PlanningScope.All);

        // Assert
        result.Value!.Diff.ShouldBe(diff);
    }

    [Fact]
    public void Plan_WithoutADialect_Fails()
    {
        // Arrange
        var sut = new MigrationPlanner(_differ, _linearizer, _projectPolicies, _planPolicies, dialect: null);

        // Act
        var result = sut.Plan(_current, _desired, PlanningScope.All);

        // Assert — a dialect is required: there is no SQL-less plan.
        result.IsFailure.ShouldBeTrue();
        result.Value.ShouldBeNull();
        result.Errors.ShouldHaveSingleItem().ShouldBe(PlanDiagnostics.MissingDialect);
    }

}
