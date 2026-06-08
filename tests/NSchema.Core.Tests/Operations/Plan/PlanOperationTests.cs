using NSchema.Operations;
using NSchema.Operations.Plan;
using NSchema.Operations.Services;
using NSchema.Plan.Model;
using NSchema.Schema;
using NSchema.Sql;
using NSchema.Sql.Model;

namespace NSchema.Tests.Operations.Plan;

public sealed class PlanOperationTests
{
    private readonly IOperationReporter _reporter = Substitute.For<IOperationReporter>();
    private readonly IMigrationHelper _helper = Substitute.For<IMigrationHelper>();
    private readonly ISqlGenerator _generator = Substitute.For<ISqlGenerator>();

    private readonly MigrationPlan _plan = new([new CreateSchema("app")], [], []);
    private readonly SqlPlan _sqlPlan = new([new SqlStatement("CREATE SCHEMA app")]);

    private PlanOperation BuildSut(ISqlGenerator? planner) =>
        new(Helpers.TestReporters.ResolverFor(_reporter), _helper, Helpers.TestSqlGenerators.ResolverFor(planner));

    private readonly PlanOperation _sut;

    public PlanOperationTests()
    {
        _helper.Plan(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(_plan);
        _generator.Generate(Arg.Any<MigrationPlan>()).Returns(_sqlPlan);

        _sut = BuildSut(_generator);
    }

    [Fact]
    public async Task Execute_PreparesPlanFromOfflineSource()
    {
        await _sut.Execute(new PlanArguments(), TestContext.Current.CancellationToken);

        await _helper.Received(1).Plan(SchemaSourceMode.Offline, required: false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_GeneratesSqlFromPlanAndReportsIt()
    {
        await _sut.Execute(new PlanArguments(), TestContext.Current.CancellationToken);

        _generator.Received(1).Generate(_plan);
        _reporter.Received(1).ReportSqlPlan(_sqlPlan);
    }

    [Fact]
    public async Task Execute_NoPlanner_ReportsPlanWithoutSqlPreview()
    {
        var sut = BuildSut(planner: null);

        await sut.Execute(new PlanArguments(), TestContext.Current.CancellationToken);

        await _helper.Received(1).Plan(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        _reporter.DidNotReceive().ReportSqlPlan(Arg.Any<SqlPlan>());
    }

    [Fact]
    public async Task Execute_PrepareThrows_DoesNotGenerateSql()
    {
        _helper.Plan(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<MigrationPlan>(_ => throw new InvalidOperationException("boom"));

        await Should.ThrowAsync<InvalidOperationException>(() => _sut.Execute(new PlanArguments()));

        _generator.DidNotReceive().Generate(Arg.Any<MigrationPlan>());
    }
}
