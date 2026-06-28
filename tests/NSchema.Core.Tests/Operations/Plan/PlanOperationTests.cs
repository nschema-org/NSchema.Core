using NSchema.Diagnostics;
using NSchema.Diff.Model;
using NSchema.Operations;
using NSchema.Operations.Plan;
using NSchema.Operations.Services;
using NSchema.Plan;
using NSchema.Plan.Model;
using NSchema.Plan.Model.Schemas;
using NSchema.Plan.PlanFile;
using NSchema.Schema;
using NSchema.Sql;
using NSchema.Sql.Model;

namespace NSchema.Tests.Operations.Plan;

public sealed class PlanOperationTests
{
    private readonly IMigrationWorkflow _workflow = Substitute.For<IMigrationWorkflow>();
    private readonly ISqlGenerator _generator = Substitute.For<ISqlGenerator>();

    private readonly MigrationPlan _plan = new([new CreateSchema("app")], [], []);
    private readonly DatabaseDiff _diff = new([]);
    private readonly SqlPlan _sqlPlan = new([new SqlStatement("CREATE SCHEMA app")]);

    private PlanOperation BuildSut(ISqlGenerator? planner) =>
        new(_workflow, new PlanFileWriter(), planner);

    private readonly PlanOperation _sut;

    public PlanOperationTests()
    {
        _workflow.ComputePlan(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(new MigrationPlanResult(_plan, _diff, []));
        _generator.Generate(Arg.Any<MigrationPlan>()).Returns(_sqlPlan);

        _sut = BuildSut(_generator);
    }

    [Fact]
    public async Task Execute_PreparesPlanFromOfflineSource()
    {
        await _sut.Execute(new PlanArguments(), TestContext.Current.CancellationToken);

        await _workflow.Received(1).ComputePlan(SchemaSourceMode.Offline, required: false, Arg.Any<string[]?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_GeneratesSqlFromPlan_AndCarriesItInTheResult()
    {
        var result = await _sut.Execute(new PlanArguments(), TestContext.Current.CancellationToken);

        // The op no longer renders; it carries the diff + generated SQL back for the caller to render.
        result.IsSuccess.ShouldBeTrue();
        result.Value.Diff.ShouldBe(_diff);
        result.Value.Sql.ShouldBe(_sqlPlan);
        _generator.Received(1).Generate(_plan);
    }

    [Fact]
    public async Task Execute_PolicyError_ReturnsFailure_CarriesDiff_WithoutGeneratingSql()
    {
        _workflow.ComputePlan(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(new MigrationPlanResult(null, _diff, [Diagnostic.Error("destructive-actions", "blocked")]));

        var result = await _sut.Execute(new PlanArguments(), TestContext.Current.CancellationToken);

        // The failure is carried back (not thrown); the diff still rides along so the caller can show it; no SQL.
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldBe("blocked");
        result.Value.ShouldNotBeNull().Diff.ShouldBe(_diff);
        result.Value.Sql.ShouldBeNull();
        _generator.DidNotReceive().Generate(Arg.Any<MigrationPlan>());
    }

    [Fact]
    public async Task Execute_NoPlanner_SucceedsWithoutSql_AndWarns()
    {
        var sut = BuildSut(planner: null);

        var result = await sut.Execute(new PlanArguments(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Sql.ShouldBeNull();
        result.Diagnostics.ShouldContain(d => d.Severity == DiagnosticSeverity.Warning && d.Message.Contains("Unable to generate SQL preview"));
    }

    [Fact]
    public async Task Execute_PrepareThrows_DoesNotGenerateSql()
    {
        // A non-policy failure (e.g. unreadable schema) still surfaces as an exception.
        _workflow.ComputePlan(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns<MigrationPlanResult>(_ => throw new InvalidOperationException("boom"));

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
    public async Task Execute_WithOutFileButNoGenerator_ReturnsFailure()
    {
        var sut = BuildSut(planner: null);

        var result = await sut.Execute(new PlanArguments { OutFile = "unused.json" }, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("requires a database provider");
    }
}
