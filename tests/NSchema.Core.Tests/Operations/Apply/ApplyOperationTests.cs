using NSchema.Operations.Apply;
using NSchema.Operations.Progress;
using NSchema.Operations.Services;
using NSchema.Sql;
using NSchema.Sql.Model;
using NSubstitute.ExceptionExtensions;

namespace NSchema.Tests.Operations.Apply;

public sealed class ApplyOperationTests
{
    private readonly IMigrationWorkflow _workflow = Substitute.For<IMigrationWorkflow>();
    private readonly IProgress<OperationProgress> _progress = Substitute.For<IProgress<OperationProgress>>();
    private readonly ISqlExecutor _executor = Substitute.For<ISqlExecutor>();

    private readonly SqlPlan _sqlPlan = new([new SqlStatement("CREATE SCHEMA app")]);

    private ApplyOperation BuildSut(ISqlExecutor? executor) => new(_workflow, _progress, executor);

    private readonly ApplyOperation _sut;

    public ApplyOperationTests() => _sut = BuildSut(_executor);

    private static ApplyArguments Args(SqlPlan sql) => new() { Sql = sql };

    [Fact]
    public async Task Execute_RunsSqlThenRefreshesState()
    {
        var result = await _sut.Execute(Args(_sqlPlan), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.AppliedSql.ShouldBe(_sqlPlan);
        result.Value!.ChangesApplied.ShouldBeTrue();
        result.Value!.StatementsExecuted.ShouldBe(1);
        await _executor.Received(1).Execute(_sqlPlan, Arg.Any<CancellationToken>());
        await _workflow.Received(1).Refresh(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_EmptyPlan_SkipsExecutionButStillRefreshes()
    {
        var result = await _sut.Execute(Args(new SqlPlan([])), TestContext.Current.CancellationToken);

        // An empty plan applied nothing, but a first run against an already-matching target still initialises the store.
        result.Value!.ChangesApplied.ShouldBeFalse();
        result.Value!.StatementsExecuted.ShouldBe(0);
        await _executor.DidNotReceive().Execute(Arg.Any<SqlPlan>(), Arg.Any<CancellationToken>());
        await _workflow.Received(1).Refresh(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenExecutionFails_StillRefreshesAndRethrows()
    {
        _executor.Execute(Arg.Any<SqlPlan>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));

        // Execution may fail partway (e.g. an un-transacted plan), so we still capture state, but the failure propagates.
        var ex = await Should.ThrowAsync<InvalidOperationException>(() => _sut.Execute(Args(_sqlPlan), TestContext.Current.CancellationToken));
        ex.Message.ShouldBe("boom");
        await _workflow.Received(1).Refresh(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_NonEmptyPlanButNoExecutor_Fails()
    {
        var sut = BuildSut(executor: null);

        var result = await sut.Execute(Args(_sqlPlan), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("requires a database provider");
    }

    [Fact]
    public async Task Execute_WhenStateRefreshFailsAfterSuccessfulApply_Surfaces()
    {
        // The migration applies cleanly, but capturing the new state fails — recording state is part of the apply, so
        // this is a real error (the database changed but the recorded state is now stale), not something to swallow.
        _workflow.Refresh(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("state store unreachable"));

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => _sut.Execute(Args(_sqlPlan), TestContext.Current.CancellationToken));

        ex.Message.ShouldBe("state store unreachable");
        await _executor.Received(1).Execute(_sqlPlan, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenBothExecutionAndStateRefreshFail_SurfacesTheExecutionError()
    {
        // A refresh failure during the best-effort capture must not mask the migration failure, which is the error the
        // operator needs to see.
        _executor.Execute(Arg.Any<SqlPlan>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("migration failed"));
        _workflow.Refresh(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("state store unreachable"));

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => _sut.Execute(Args(_sqlPlan), TestContext.Current.CancellationToken));

        ex.Message.ShouldBe("migration failed");
        await _workflow.Received(1).Refresh(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_EmptyPlan_WhenStateRefreshFails_Surfaces()
    {
        // Even with nothing to execute, the state capture is part of the apply; its failure is surfaced.
        _workflow.Refresh(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("state store unreachable"));

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => _sut.Execute(Args(new SqlPlan([])), TestContext.Current.CancellationToken));

        ex.Message.ShouldBe("state store unreachable");
    }
}
