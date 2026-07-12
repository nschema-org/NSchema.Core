using NSchema.Diagnostics;
using NSchema.Diff;
using NSchema.Diff.Model;
using NSchema.Operations.Apply;
using NSchema.Operations.Progress;
using NSchema.Operations.Services;
using NSchema.Plan.Model;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Scripts;
using NSchema.Sql;
using NSchema.Sql.Model;
using NSchema.State.Storage;
using NSubstitute.ExceptionExtensions;

namespace NSchema.Tests.Operations.Apply;

public sealed class ApplyOperationTests
{
    private readonly IMigrationWorkflow _workflow = Substitute.For<IMigrationWorkflow>();
    private readonly IProgress<OperationProgress> _progress = Substitute.For<IProgress<OperationProgress>>();
    private readonly ISqlExecutor _executor = Substitute.For<ISqlExecutor>();
    private readonly List<IDiffPolicy> _diffPolicies = [];
    private readonly ISchemaStateManager _stateManager = Substitute.For<ISchemaStateManager>();

    private readonly MigrationPlan _plan = new(new DatabaseDiff([]), [new SqlStatement("CREATE SCHEMA app")]);
    private static readonly MigrationPlan _emptyPlan = new(new DatabaseDiff([]), []);

    private ApplyOperation BuildSut(ISqlExecutor? executor) => new(_workflow, _progress, _diffPolicies, _stateManager, executor);

    private readonly ApplyOperation _sut;

    public ApplyOperationTests()
    {
        _stateManager.IsConfigured.Returns(true);
        _workflow.Refresh(Arg.Any<MigrationPlan?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new StateCapture(new DatabaseSchema([]), 0)));
        _sut = BuildSut(_executor);
    }

    [Fact]
    public async Task Execute_WithoutAStateStore_FailsBeforeExecutingAnything()
    {
        // Arrange — applying records the run (capture + run-once ledger), so an unrecordable run is refused.
        _stateManager.IsConfigured.Returns(false);

        // Act
        var result = await _sut.Execute(Args(_plan), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("requires a state store");
        await _executor.DidNotReceive().Execute(Arg.Any<IReadOnlyList<SqlStatement>>(), Arg.Any<CancellationToken>());
    }

    private static ApplyArguments Args(MigrationPlan plan) => new() { Plan = plan };

    [Fact]
    public async Task Execute_RunsSqlThenRefreshesState()
    {
        var result = await _sut.Execute(Args(_plan), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.AppliedPlan.ShouldBe(_plan);
        result.Value!.ChangesApplied.ShouldBeTrue();
        result.Value!.StatementsExecuted.ShouldBe(1);
        await _executor.Received(1).Execute(_plan.Statements, Arg.Any<CancellationToken>());
        await _workflow.Received(1).Refresh(Arg.Any<MigrationPlan?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_EmptyPlan_SkipsExecutionButStillRefreshes()
    {
        var result = await _sut.Execute(Args(_emptyPlan), TestContext.Current.CancellationToken);

        // An empty plan applied nothing, but a first run against an already-matching target still initialises the store.
        result.Value!.ChangesApplied.ShouldBeFalse();
        result.Value!.StatementsExecuted.ShouldBe(0);
        await _executor.DidNotReceive().Execute(Arg.Any<IReadOnlyList<SqlStatement>>(), Arg.Any<CancellationToken>());
        await _workflow.Received(1).Refresh(Arg.Any<MigrationPlan?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_Success_PassesTheAppliedPlanToTheCapture()
    {
        // Arrange — the plan carries its scripts whole; the capture derives the run-once ledger records from it.
        var plan = _plan with { Diff = new DatabaseDiff([]) { Scripts = [new Script("seed", "SELECT 1", new DeploymentEvent(DeploymentPhase.Post))] } };

        // Act
        await _sut.Execute(Args(plan), TestContext.Current.CancellationToken);

        // Assert — the successful capture receives exactly the plan that ran.
        await _workflow.Received(1).Refresh(plan, Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_Failure_DoesNotPassTheAppliedPlan()
    {
        // Arrange — whether the plan's run-once scripts ran before the failure is unknowable, so nothing is recorded.
        _executor.Execute(Arg.Any<IReadOnlyList<SqlStatement>>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));
        var plan = _plan with { Diff = new DatabaseDiff([]) { Scripts = [new Script("seed", "SELECT 1", new DeploymentEvent(DeploymentPhase.Post))] } };

        // Act
        await Should.ThrowAsync<InvalidOperationException>(() => _sut.Execute(Args(plan), TestContext.Current.CancellationToken));

        // Assert
        await _workflow.Received(1).Refresh((MigrationPlan?)null, Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenExecutionFails_StillRefreshesAndRethrows()
    {
        _executor.Execute(Arg.Any<IReadOnlyList<SqlStatement>>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));

        // Execution may fail partway (e.g. an un-transacted plan), so we still capture state, but the failure propagates.
        var ex = await Should.ThrowAsync<InvalidOperationException>(() => _sut.Execute(Args(_plan), TestContext.Current.CancellationToken));
        ex.Message.ShouldBe("boom");
        await _workflow.Received(1).Refresh(Arg.Any<MigrationPlan?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_NonEmptyPlanButNoExecutor_Fails()
    {
        var sut = BuildSut(executor: null);

        var result = await sut.Execute(Args(_plan), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("requires a database provider");
    }

    [Fact]
    public async Task Execute_WhenStateRefreshFailsAfterSuccessfulApply_Surfaces()
    {
        // The migration applies cleanly, but capturing the new state fails — recording state is part of the apply, so
        // this is a real error (the database changed but the recorded state is now stale), not something to swallow.
        _workflow.Refresh(Arg.Any<MigrationPlan?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("state store unreachable"));

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => _sut.Execute(Args(_plan), TestContext.Current.CancellationToken));

        ex.Message.ShouldBe("state store unreachable");
        await _executor.Received(1).Execute(_plan.Statements, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenBothExecutionAndStateRefreshFail_SurfacesTheExecutionError()
    {
        // A refresh failure during the best-effort capture must not mask the migration failure, which is the error the
        // operator needs to see.
        _executor.Execute(Arg.Any<IReadOnlyList<SqlStatement>>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("migration failed"));
        _workflow.Refresh(Arg.Any<MigrationPlan?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("state store unreachable"));

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => _sut.Execute(Args(_plan), TestContext.Current.CancellationToken));

        ex.Message.ShouldBe("migration failed");
        await _workflow.Received(1).Refresh(Arg.Any<MigrationPlan?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_PolicyBlockedPlan_IsNotApplied()
    {
        // Arrange — policies are enforced at the execution point against the carried diff, so a saved plan
        // (older config, other tooling, hand edits) cannot slip past them.
        var policy = Substitute.For<IDiffPolicy>();
        policy.Validate(_plan.Diff).Returns([Diagnostic.Error("destructive", "drops table")]);
        _diffPolicies.Add(policy);

        // Act
        var result = await _sut.Execute(Args(_plan), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(d => d.Message == "drops table");
        result.Errors.ShouldContain(d => d.Message.Contains("blocked by policy"));
        await _executor.DidNotReceive().Execute(Arg.Any<IReadOnlyList<SqlStatement>>(), Arg.Any<CancellationToken>());
        await _workflow.DidNotReceive().Refresh(Arg.Any<MigrationPlan?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_PolicyBlockedPlan_WithForce_AppliesWithTheErrorsDemotedToWarnings()
    {
        // Arrange — force is an informed override: the findings stay visible, demoted so they don't fail the run.
        var policy = Substitute.For<IDiffPolicy>();
        policy.Validate(_plan.Diff).Returns([Diagnostic.Error("destructive", "drops table")]);
        _diffPolicies.Add(policy);

        // Act
        var result = await _sut.Execute(new ApplyArguments { Plan = _plan, Force = true }, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.ShouldContain(d => d.Message == "drops table" && d.Severity == DiagnosticSeverity.Warning);
        await _executor.Received(1).Execute(_plan.Statements, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_PolicyWarnings_RideTheResultAndDoNotBlock()
    {
        // Arrange
        var policy = Substitute.For<IDiffPolicy>();
        policy.Validate(_plan.Diff).Returns([Diagnostic.Warning("data-hazards", "risky add")]);
        _diffPolicies.Add(policy);

        // Act
        var result = await _sut.Execute(Args(_plan), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.ShouldContain(d => d.Message == "risky add");
        await _executor.Received(1).Execute(_plan.Statements, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_EmptyPlan_WhenStateRefreshFails_Surfaces()
    {
        // Even with nothing to execute, the state capture is part of the apply; its failure is surfaced.
        _workflow.Refresh(Arg.Any<MigrationPlan?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("state store unreachable"));

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => _sut.Execute(Args(_emptyPlan), TestContext.Current.CancellationToken));

        ex.Message.ShouldBe("state store unreachable");
    }
}
