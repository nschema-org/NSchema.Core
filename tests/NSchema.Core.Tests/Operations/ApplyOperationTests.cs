using NSchema.Operations;
using NSchema.Operations.Services;
using NSchema.Plan.Model;
using NSchema.Schema;
using NSchema.Sql;
using NSchema.Sql.Model;
using NSubstitute.ExceptionExtensions;

namespace NSchema.Tests.Operations;

public sealed class ApplyOperationTests
{
    private readonly IOperationReporter _reporter = Substitute.For<IOperationReporter>();
    private readonly IMigrationHelper _helper = Substitute.For<IMigrationHelper>();
    private readonly ISqlGenerator _generator = Substitute.For<ISqlGenerator>();
    private readonly ISqlExecutor _executor = Substitute.For<ISqlExecutor>();
    private readonly IOperationConfirmation _confirmation = Substitute.For<IOperationConfirmation>();

    private readonly MigrationPlan _plan = new([new CreateSchema("app")], [], []);
    private readonly SqlPlan _sqlPlan = new([new SqlStatement("CREATE SCHEMA app")]);

    private ApplyOperation BuildSut(ISqlGenerator? planner, ISqlExecutor? executor) => new(
        Helpers.TestReporters.ResolverFor(_reporter),
        _confirmation, _helper,
        Helpers.TestSqlGenerators.ResolverFor(planner),
        executor
    );

    private readonly ApplyOperation _sut;

    public ApplyOperationTests()
    {
        _helper.Plan(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(_plan);
        _helper.HasStore.Returns(true);
        _generator.Generate(Arg.Any<MigrationPlan>()).Returns(_sqlPlan);
        _confirmation.Confirm(Arg.Any<MigrationPlan>(), Arg.Any<CancellationToken>()).Returns(true);

        _sut = BuildSut(_generator, _executor);
    }

    [Fact]
    public async Task Execute_PreparesPlanFromOnlineSource()
    {
        await _sut.Execute(TestContext.Current.CancellationToken);

        await _helper.Received(1).Plan(SchemaSourceMode.Online, required: true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_GeneratesAndExecutesSqlPlan()
    {
        await _sut.Execute(TestContext.Current.CancellationToken);

        _generator.Received(1).Generate(_plan);
        await _executor.Received(1).Execute(_sqlPlan, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_NoPlanner_ThrowsWithoutPreparing()
    {
        var sut = BuildSut(planner: null, executor: _executor);

        await Should.ThrowAsync<InvalidOperationException>(() => sut.Execute());
        await _helper.DidNotReceive().Plan(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_NoExecutor_ThrowsWithoutPreparing()
    {
        var sut = BuildSut(planner: _generator, executor: null);

        await Should.ThrowAsync<InvalidOperationException>(() => sut.Execute());
        await _helper.DidNotReceive().Plan(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WithStore_RefreshesStateAfterSuccess()
    {
        await _sut.Execute(TestContext.Current.CancellationToken);

        await _helper.Received(1).Refresh(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_NoStore_DoesNotRefresh()
    {
        _helper.HasStore.Returns(false);

        await _sut.Execute(TestContext.Current.CancellationToken);

        await _helper.DidNotReceive().Refresh(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ExecutionFails_DoesNotRefresh()
    {
        _executor.Execute(Arg.Any<SqlPlan>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));

        await Should.ThrowAsync<InvalidOperationException>(() => _sut.Execute(TestContext.Current.CancellationToken));

        await _helper.DidNotReceive().Refresh(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_NotConfirmed_DoesNotExecuteOrRefresh()
    {
        _confirmation.Confirm(Arg.Any<MigrationPlan>(), Arg.Any<CancellationToken>()).Returns(false);

        await _sut.Execute(TestContext.Current.CancellationToken);

        await _executor.DidNotReceive().Execute(Arg.Any<SqlPlan>(), Arg.Any<CancellationToken>());
        await _helper.DidNotReceive().Refresh(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_Confirmed_Executes()
    {
        _confirmation.Confirm(Arg.Any<MigrationPlan>(), Arg.Any<CancellationToken>()).Returns(true);

        await _sut.Execute(TestContext.Current.CancellationToken);

        await _executor.Received(1).Execute(_sqlPlan, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ConfirmationPromptedAfterSqlReported()
    {
        var callOrder = new List<string>();
        _reporter.When(r => r.ReportSqlPlan(Arg.Any<SqlPlan>())).Do(_ => callOrder.Add("sql"));
        _confirmation.Confirm(Arg.Any<MigrationPlan>(), Arg.Any<CancellationToken>())
            .Returns(_ => { callOrder.Add("confirm"); return true; });

        await _sut.Execute(TestContext.Current.CancellationToken);

        callOrder.ShouldBe(["sql", "confirm"]);
    }
}
