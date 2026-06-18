using NSchema.Diff.Model;
using NSchema.Operations;
using NSchema.Operations.Apply;
using NSchema.Operations.Confirmation;
using NSchema.Operations.Services;
using NSchema.Plan.Model;
using NSchema.Plan.Model.Schemas;
using NSchema.Plan.PlanFile;
using NSchema.Schema;
using NSchema.Sql;
using NSchema.Sql.Model;
using NSchema.State.Model;
using NSchema.Tests.Helpers;
using NSubstitute.ExceptionExtensions;

namespace NSchema.Tests.Operations.Apply;

public sealed class ApplyOperationTests
{
    private readonly IOperationReporter _reporter = Substitute.For<IOperationReporter>();
    private readonly IMigrationWorkflow _workflow = Substitute.For<IMigrationWorkflow>();
    private readonly ISqlGenerator _generator = Substitute.For<ISqlGenerator>();
    private readonly ISqlExecutor _executor = Substitute.For<ISqlExecutor>();
    private readonly IOperationConfirmation _confirmation = Substitute.For<IOperationConfirmation>();
    private readonly RecordingStateLock _stateLock = new();

    private readonly MigrationPlan _plan = new([new CreateSchema("app")], [], []);
    private readonly DatabaseDiff _diff = new([]);
    private readonly SqlPlan _sqlPlan = new([new SqlStatement("CREATE SCHEMA app")]);

    private ApplyOperation BuildSut(ISqlGenerator? planner, ISqlExecutor? executor) => new(
        _reporter,
        _confirmation, _workflow,
        _stateLock,
        new PlanFileWriter(),
        planner,
        executor
    );

    private readonly ApplyOperation _sut;

    public ApplyOperationTests()
    {
        _workflow.Plan(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(new PlannedMigration(_plan, _diff));
        _generator.Generate(Arg.Any<MigrationPlan>()).Returns(_sqlPlan);
        _confirmation.Confirm(Arg.Any<OperationConfirmationRequest>(), Arg.Any<CancellationToken>()).Returns(true);

        _sut = BuildSut(_generator, _executor);
    }

    [Fact]
    public async Task Execute_PreparesPlanFromOnlineSource()
    {
        await _sut.Execute(new ApplyArguments(), TestContext.Current.CancellationToken);

        await _workflow.Received(1).Plan(SchemaSourceMode.Online, required: true, Arg.Any<string[]?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_GeneratesAndExecutesSqlPlan()
    {
        await _sut.Execute(new ApplyArguments(), TestContext.Current.CancellationToken);

        _generator.Received(1).Generate(_plan);
        await _executor.Received(1).Execute(_sqlPlan, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_NoPlanner_ThrowsWithoutPreparing()
    {
        var sut = BuildSut(planner: null, executor: _executor);

        await Should.ThrowAsync<InvalidOperationException>(() => sut.Execute(new ApplyArguments()));
        await _workflow.DidNotReceive().Plan(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_NoExecutor_ThrowsWithoutPreparing()
    {
        var sut = BuildSut(planner: _generator, executor: null);

        await Should.ThrowAsync<InvalidOperationException>(() => sut.Execute(new ApplyArguments()));
        await _workflow.DidNotReceive().Plan(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WithStore_RefreshesStateAfterSuccess()
    {
        await _sut.Execute(new ApplyArguments(), TestContext.Current.CancellationToken);

        await _workflow.Received(1).Refresh(RefreshMode.Optional, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ExecutionFails_StillRefreshesAndRethrows()
    {
        _executor.Execute(Arg.Any<SqlPlan>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));

        // Execution may fail partway (e.g. an un-transacted plan), so we still capture state, but the original failure propagates.
        var ex = await Should.ThrowAsync<InvalidOperationException>(() => _sut.Execute(new ApplyArguments(), TestContext.Current.CancellationToken));
        ex.Message.ShouldBe("boom");

        await _workflow.Received(1).Refresh(RefreshMode.Optional, Arg.Any<CancellationToken>());
        _stateLock.Released.ShouldBe(1); // lock released even when execution fails
    }

    [Fact]
    public async Task Execute_AcquiresAndReleasesStateLock()
    {
        await _sut.Execute(new ApplyArguments(), TestContext.Current.CancellationToken);

        _stateLock.Acquisitions.ShouldHaveSingleItem().Operation.ShouldBe("apply");
        _stateLock.Released.ShouldBe(1);
    }

    [Fact]
    public async Task Execute_StateLocked_DoesNotPlanOrExecute()
    {
        _stateLock.OnAcquire = _ => throw new StateLockedException("locked");

        await Should.ThrowAsync<StateLockedException>(() => _sut.Execute(new ApplyArguments()));

        await _workflow.DidNotReceive().Plan(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>());
        await _executor.DidNotReceive().Execute(Arg.Any<SqlPlan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_NotConfirmed_DoesNotExecuteOrRefresh()
    {
        _confirmation.Confirm(Arg.Any<OperationConfirmationRequest>(), Arg.Any<CancellationToken>()).Returns(false);

        await _sut.Execute(new ApplyArguments(), TestContext.Current.CancellationToken);

        await _executor.DidNotReceive().Execute(Arg.Any<SqlPlan>(), Arg.Any<CancellationToken>());
        await _workflow.DidNotReceive().Refresh(Arg.Any<RefreshMode>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_Confirmed_Executes()
    {
        _confirmation.Confirm(Arg.Any<OperationConfirmationRequest>(), Arg.Any<CancellationToken>()).Returns(true);

        await _sut.Execute(new ApplyArguments(), TestContext.Current.CancellationToken);

        await _executor.Received(1).Execute(_sqlPlan, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ConfirmsWithApplyRequestCarryingThePlan()
    {
        await _sut.Execute(new ApplyArguments(), TestContext.Current.CancellationToken);

        await _confirmation.Received(1).Confirm(
            Arg.Is<ApplyConfirmationRequest>(r => r.Plan == _plan && !r.IsDestructive),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ConfirmationPromptedAfterSqlReported()
    {
        var callOrder = new List<string>();
        _reporter.When(r => r.ReportSqlPlan(Arg.Any<SqlPlan>())).Do(_ => callOrder.Add("sql"));
        _confirmation.Confirm(Arg.Any<OperationConfirmationRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => { callOrder.Add("confirm"); return true; });

        await _sut.Execute(new ApplyArguments(), TestContext.Current.CancellationToken);

        callOrder.ShouldBe(["sql", "confirm"]);
    }

    private string WritePlanFile(SqlPlan? savedSql = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"nschema-plan-{Guid.NewGuid():N}.json");
        var envelope = new PlanFileEnvelope(_plan, savedSql ?? _sqlPlan, _diff, DateTimeOffset.UnixEpoch);
        File.WriteAllBytes(path, new PlanFileWriter().Serialize(envelope).ToArray());
        return path;
    }

    [Fact]
    public async Task Execute_WithPlanFile_ExecutesSavedSqlWithoutReplanning()
    {
        var path = WritePlanFile();
        try
        {
            await _sut.Execute(new ApplyArguments { PlanFile = path }, TestContext.Current.CancellationToken);

            // The whole point: no fresh plan is computed; the saved SQL is what runs, then state is captured.
            await _workflow.DidNotReceive().Plan(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>());
            await _executor.Received(1).Execute(Arg.Is<SqlPlan>(p => p.Statements.SequenceEqual(_sqlPlan.Statements)), Arg.Any<CancellationToken>());
            await _workflow.Received(1).Refresh(RefreshMode.Optional, Arg.Any<CancellationToken>());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Execute_WithPlanFile_AcquiresLockAndConfirmsBeforeExecuting()
    {
        var path = WritePlanFile();
        try
        {
            await _sut.Execute(new ApplyArguments { PlanFile = path }, TestContext.Current.CancellationToken);

            _stateLock.Acquisitions.ShouldHaveSingleItem().Operation.ShouldBe("apply");
            await _confirmation.Received(1).Confirm(Arg.Any<ApplyConfirmationRequest>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Execute_WithPlanFile_ReportsSavedDiffPlanAndSql()
    {
        var path = WritePlanFile();
        try
        {
            await _sut.Execute(new ApplyArguments { PlanFile = path }, TestContext.Current.CancellationToken);

            // Applying a saved plan shows the same diff/plan/SQL view the plan step produced.
            _reporter.Received(1).ReportDiff(Arg.Any<DatabaseDiff>());
            _reporter.Received(1).ReportPlan(Arg.Any<MigrationPlan>());
            _reporter.Received(1).ReportSqlPlan(Arg.Any<SqlPlan>());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Execute_WithPlanFile_NotConfirmed_DoesNotExecute()
    {
        _confirmation.Confirm(Arg.Any<OperationConfirmationRequest>(), Arg.Any<CancellationToken>()).Returns(false);
        var path = WritePlanFile();
        try
        {
            await _sut.Execute(new ApplyArguments { PlanFile = path }, TestContext.Current.CancellationToken);

            await _executor.DidNotReceive().Execute(Arg.Any<SqlPlan>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
