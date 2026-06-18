using NSchema.Diff.Model;
using NSchema.Operations;
using NSchema.Operations.Plan;
using NSchema.Operations.Services;
using NSchema.Plan.Model;
using NSchema.Plan.Model.Schemas;
using NSchema.Plan.PlanFile;
using NSchema.Schema;
using NSchema.Sql;
using NSchema.Sql.Model;

namespace NSchema.Tests.Operations.Plan;

public sealed class PlanOperationTests
{
    private readonly IOperationReporter _reporter = Substitute.For<IOperationReporter>();
    private readonly IMigrationWorkflow _workflow = Substitute.For<IMigrationWorkflow>();
    private readonly ISqlGenerator _generator = Substitute.For<ISqlGenerator>();

    private readonly MigrationPlan _plan = new([new CreateSchema("app")], [], []);
    private readonly DatabaseDiff _diff = new([]);
    private readonly SqlPlan _sqlPlan = new([new SqlStatement("CREATE SCHEMA app")]);

    private PlanOperation BuildSut(ISqlGenerator? planner) =>
        new(_reporter, _workflow, new PlanFileWriter(), planner);

    private readonly PlanOperation _sut;

    public PlanOperationTests()
    {
        _workflow.Plan(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(new PlannedMigration(_plan, _diff));
        _generator.Generate(Arg.Any<MigrationPlan>()).Returns(_sqlPlan);

        _sut = BuildSut(_generator);
    }

    [Fact]
    public async Task Execute_PreparesPlanFromOfflineSource()
    {
        await _sut.Execute(new PlanArguments(), TestContext.Current.CancellationToken);

        await _workflow.Received(1).Plan(SchemaSourceMode.Offline, required: false, Arg.Any<string[]?>(), Arg.Any<CancellationToken>());
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

        await _workflow.Received(1).Plan(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>());
        _reporter.DidNotReceive().ReportSqlPlan(Arg.Any<SqlPlan>());
    }

    [Fact]
    public async Task Execute_PrepareThrows_DoesNotGenerateSql()
    {
        _workflow.Plan(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns<PlannedMigration>(_ => throw new InvalidOperationException("boom"));

        await Should.ThrowAsync<InvalidOperationException>(() => _sut.Execute(new PlanArguments()));

        _generator.DidNotReceive().Generate(Arg.Any<MigrationPlan>());
    }

    [Fact]
    public async Task Execute_WithOutFile_WritesAnApplyablePlanFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nschema-plan-{Guid.NewGuid():N}.json");
        try
        {
            await _sut.Execute(new PlanArguments { OutFile = path }, TestContext.Current.CancellationToken);

            File.Exists(path).ShouldBeTrue();
            var envelope = new PlanFileWriter().Deserialize(await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
            envelope.Version.ShouldBe(PlanFileEnvelope.CurrentVersion);
            envelope.Sql.Statements.ShouldBe(_sqlPlan.Statements);
            envelope.Plan.Actions.ShouldBe(_plan.Actions);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Execute_WithOutFileButNoGenerator_Throws()
    {
        var sut = BuildSut(planner: null);

        await Should.ThrowAsync<InvalidOperationException>(
            () => sut.Execute(new PlanArguments { OutFile = "unused.json" }));
    }
}
