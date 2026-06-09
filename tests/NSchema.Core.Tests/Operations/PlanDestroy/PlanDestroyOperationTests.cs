using NSchema.Diff.Model;
using NSchema.Operations;
using NSchema.Operations.PlanDestroy;
using NSchema.Operations.Services;
using NSchema.Plan.Model;
using NSchema.Plan.PlanFile;
using NSchema.Sql;
using NSchema.Sql.Model;

namespace NSchema.Tests.Operations.PlanDestroy;

public sealed class PlanDestroyOperationTests
{
    private readonly IOperationReporter _reporter = Substitute.For<IOperationReporter>();
    private readonly IMigrationWorkflow _workflow = Substitute.For<IMigrationWorkflow>();
    private readonly ISqlGenerator _generator = Substitute.For<ISqlGenerator>();

    private readonly MigrationPlan _plan = new([new DropSchema("app")], [], []);
    private readonly DatabaseDiff _diff = new([]);
    private readonly SqlPlan _sqlPlan = new([new SqlStatement("DROP SCHEMA app")]);

    private PlanDestroyOperation BuildSut(ISqlGenerator? generator) =>
        new(Helpers.TestReporters.ResolverFor(_reporter), _workflow, Helpers.TestSqlGenerators.ResolverFor(generator),
            new PlanFileWriter());

    private readonly PlanDestroyOperation _sut;

    public PlanDestroyOperationTests()
    {
        _workflow.PlanDestroy(Arg.Any<CancellationToken>()).Returns(new PlannedMigration(_plan, _diff));
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
            .Returns<PlannedMigration>(_ => throw new InvalidOperationException("boom"));

        await Should.ThrowAsync<InvalidOperationException>(() => _sut.Execute(new PlanDestroyArguments()));

        _generator.DidNotReceive().Generate(Arg.Any<MigrationPlan>());
    }

    [Fact]
    public async Task Execute_WithOutFile_WritesTheTeardownPlanFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nschema-destroy-{Guid.NewGuid():N}.json");
        try
        {
            await _sut.Execute(new PlanDestroyArguments { OutFile = path }, TestContext.Current.CancellationToken);

            var envelope = new PlanFileWriter().Deserialize(await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
            envelope.Plan.Actions.ShouldBe(_plan.Actions);
            envelope.Sql.Statements.ShouldBe(_sqlPlan.Statements);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
