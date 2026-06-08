using NSchema.Operations;
using NSchema.Operations.PlanDestroy;
using NSchema.Operations.Services;
using NSchema.Plan.Model;
using NSchema.Sql;
using NSchema.Sql.Model;

namespace NSchema.Tests.Operations.PlanDestroy;

public sealed class PlanDestroyOperationTests
{
    private readonly IOperationReporter _reporter = Substitute.For<IOperationReporter>();
    private readonly IMigrationWorkflow _workflow = Substitute.For<IMigrationWorkflow>();
    private readonly ISqlGenerator _generator = Substitute.For<ISqlGenerator>();

    private readonly MigrationPlan _plan = new([new DropSchema("app")], [], []);
    private readonly SqlPlan _sqlPlan = new([new SqlStatement("DROP SCHEMA app")]);

    private PlanDestroyOperation BuildSut(ISqlGenerator? generator) =>
        new(Helpers.TestReporters.ResolverFor(_reporter), _workflow, Helpers.TestSqlGenerators.ResolverFor(generator));

    private readonly PlanDestroyOperation _sut;

    public PlanDestroyOperationTests()
    {
        _workflow.PlanDestroy(Arg.Any<CancellationToken>()).Returns(_plan);
        _generator.Generate(Arg.Any<MigrationPlan>()).Returns(_sqlPlan);

        _sut = BuildSut(_generator);
    }

    [Fact]
    public async Task Execute_PlansTeardownViaTheTrustedPath()
    {
        await _sut.Execute(new PlanDestroyArguments(), TestContext.Current.CancellationToken);

        await _workflow.Received(1).PlanDestroy(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_GeneratesSqlFromPlanAndReportsIt()
    {
        await _sut.Execute(new PlanDestroyArguments(), TestContext.Current.CancellationToken);

        _generator.Received(1).Generate(_plan);
        _reporter.Received(1).ReportSqlPlan(_sqlPlan);
    }

    [Fact]
    public async Task Execute_DoesNotExecuteOrCaptureState()
    {
        await _sut.Execute(new PlanDestroyArguments(), TestContext.Current.CancellationToken);

        // Preview only: no confirmation, no execution, and no post-run state capture.
        await _workflow.DidNotReceive().Refresh(Arg.Any<RefreshMode>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_NoGenerator_ReportsPlanWithoutSqlPreview()
    {
        var sut = BuildSut(generator: null);

        await sut.Execute(new PlanDestroyArguments(), TestContext.Current.CancellationToken);

        await _workflow.Received(1).PlanDestroy(Arg.Any<CancellationToken>());
        _reporter.DidNotReceive().ReportSqlPlan(Arg.Any<SqlPlan>());
    }

    [Fact]
    public async Task Execute_PlanThrows_DoesNotGenerateSql()
    {
        _workflow.PlanDestroy(Arg.Any<CancellationToken>())
            .Returns<MigrationPlan>(_ => throw new InvalidOperationException("boom"));

        await Should.ThrowAsync<InvalidOperationException>(() => _sut.Execute(new PlanDestroyArguments()));

        _generator.DidNotReceive().Generate(Arg.Any<MigrationPlan>());
    }
}
