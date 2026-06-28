using NSchema.Diff.Model;
using NSchema.Operations;
using NSchema.Operations.Progress;
using NSchema.Operations.Confirmation;
using NSchema.Operations.Destroy;
using NSchema.Operations.Services;
using NSchema.Plan;
using NSchema.Plan.Model;
using NSchema.Plan.Model.Schemas;
using NSchema.Sql;
using NSchema.Sql.Model;
using NSchema.State.Model;
using NSchema.Tests.Helpers;
using NSubstitute.ExceptionExtensions;

namespace NSchema.Tests.Operations.Destroy;

public sealed class DestroyOperationTests
{
    private readonly IOperationReporter _reporter = Substitute.For<IOperationReporter>();
    private readonly IProgress<OperationProgress> _progress = Substitute.For<IProgress<OperationProgress>>();
    private readonly IMigrationWorkflow _workflow = Substitute.For<IMigrationWorkflow>();
    private readonly ISqlGenerator _generator = Substitute.For<ISqlGenerator>();
    private readonly ISqlExecutor _executor = Substitute.For<ISqlExecutor>();
    private readonly IOperationConfirmation _confirmation = Substitute.For<IOperationConfirmation>();
    private readonly RecordingStateLock _stateLock = new();

    private readonly MigrationPlan _plan = new([new DropSchema("app")], [], []);
    private readonly DatabaseDiff _diff = new([]);
    private readonly SqlPlan _sqlPlan = new([new SqlStatement("DROP SCHEMA app")]);

    private DestroyOperation BuildSut(ISqlGenerator? generator, ISqlExecutor? executor) => new(
        _reporter,
        _progress,
        _confirmation, _workflow,
        _stateLock,
        generator,
        executor
    );

    private readonly DestroyOperation _sut;

    public DestroyOperationTests()
    {
        _workflow.ComputeTeardown(Arg.Any<CancellationToken>()).Returns(new MigrationPlanResult(_plan, _diff, []));
        _generator.Generate(Arg.Any<MigrationPlan>()).Returns(_sqlPlan);
        _confirmation.Confirm(Arg.Any<OperationConfirmationRequest>(), Arg.Any<CancellationToken>()).Returns(true);

        _sut = BuildSut(_generator, _executor);
    }

    [Fact]
    public async Task Execute_GeneratesAndExecutesTeardownSql()
    {
        await _sut.Execute(new DestroyArguments(), TestContext.Current.CancellationToken);

        await _workflow.Received(1).ComputeTeardown(Arg.Any<CancellationToken>());
        _generator.Received(1).Generate(_plan);
        await _executor.Received(1).Execute(_sqlPlan, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_NoGenerator_FailsWithoutPlanning()
    {
        var sut = BuildSut(generator: null, executor: _executor);

        var result = await sut.Execute(new DestroyArguments(), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        await _workflow.DidNotReceive().ComputeTeardown(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_NoExecutor_FailsWithoutPlanning()
    {
        var sut = BuildSut(generator: _generator, executor: null);

        var result = await sut.Execute(new DestroyArguments(), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        await _workflow.DidNotReceive().ComputeTeardown(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WithStore_RefreshesStateAfterSuccess()
    {
        await _sut.Execute(new DestroyArguments(), TestContext.Current.CancellationToken);

        await _workflow.Received(1).Refresh(RefreshMode.Optional, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ExecutionFails_StillRefreshesAndRethrows()
    {
        _executor.Execute(Arg.Any<SqlPlan>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));

        // Teardown may fail partway (e.g. an un-transacted plan), so we still capture state, but the original failure propagates.
        var ex = await Should.ThrowAsync<InvalidOperationException>(() => _sut.Execute(new DestroyArguments(), TestContext.Current.CancellationToken));
        ex.Message.ShouldBe("boom");

        await _workflow.Received(1).Refresh(RefreshMode.Optional, Arg.Any<CancellationToken>());
        _stateLock.Released.ShouldBe(1); // lock released even when teardown fails
    }

    [Fact]
    public async Task Execute_AcquiresAndReleasesStateLock()
    {
        await _sut.Execute(new DestroyArguments(), TestContext.Current.CancellationToken);

        _stateLock.Acquisitions.ShouldHaveSingleItem().Operation.ShouldBe("destroy");
        _stateLock.Released.ShouldBe(1);
    }

    [Fact]
    public async Task Execute_StateLocked_DoesNotPlanOrExecute()
    {
        _stateLock.OnAcquire = _ => throw new StateLockedException("locked");

        await Should.ThrowAsync<StateLockedException>(() => _sut.Execute(new DestroyArguments()));

        await _workflow.DidNotReceive().ComputeTeardown(Arg.Any<CancellationToken>());
        await _executor.DidNotReceive().Execute(Arg.Any<SqlPlan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_NotConfirmed_DoesNotExecuteOrRefresh()
    {
        _confirmation.Confirm(Arg.Any<OperationConfirmationRequest>(), Arg.Any<CancellationToken>()).Returns(false);

        await _sut.Execute(new DestroyArguments(), TestContext.Current.CancellationToken);

        await _executor.DidNotReceive().Execute(Arg.Any<SqlPlan>(), Arg.Any<CancellationToken>());
        await _workflow.DidNotReceive().Refresh(Arg.Any<RefreshMode>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_Confirmed_Executes()
    {
        _confirmation.Confirm(Arg.Any<OperationConfirmationRequest>(), Arg.Any<CancellationToken>()).Returns(true);

        await _sut.Execute(new DestroyArguments(), TestContext.Current.CancellationToken);

        await _executor.Received(1).Execute(_sqlPlan, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ReportsOutcomeSummaryWithCountsAndStatements()
    {
        var diff = new DatabaseDiff([new SchemaDiff("app", ChangeKind.Remove, null, null, [], [])]);
        _workflow.ComputeTeardown(Arg.Any<CancellationToken>()).Returns(new MigrationPlanResult(_plan, diff, []));

        await _sut.Execute(new DestroyArguments(), TestContext.Current.CancellationToken);

        _reporter.Received().Report(MessageKind.Success, "Destroy complete. 1 destroyed (1 statement).");
    }

    [Fact]
    public async Task Execute_ConfirmsWithDestructiveDestroyRequestCarryingThePlan()
    {
        await _sut.Execute(new DestroyArguments(), TestContext.Current.CancellationToken);

        await _confirmation.Received(1).Confirm(
            Arg.Is<DestroyConfirmationRequest>(r => r.Plan == _plan && r.IsDestructive),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ConfirmationPromptedAfterSqlReported()
    {
        var callOrder = new List<string>();
        _reporter.When(r => r.ReportSqlPlan(Arg.Any<SqlPlan>())).Do(_ => callOrder.Add("sql"));
        _confirmation.Confirm(Arg.Any<OperationConfirmationRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => { callOrder.Add("confirm"); return true; });

        await _sut.Execute(new DestroyArguments(), TestContext.Current.CancellationToken);

        callOrder.ShouldBe(["sql", "confirm"]);
    }
}
