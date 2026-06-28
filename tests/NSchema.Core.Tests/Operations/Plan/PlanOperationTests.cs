using NSchema.Diagnostics;
using NSchema.Diff.Model;
using NSchema.Operations.Plan;
using NSchema.Operations.Services;
using NSchema.Plan;
using NSchema.Plan.Model;
using NSchema.Plan.PlanFile;
using NSchema.Schema;
using NSchema.Sql;
using NSchema.Sql.Model;

namespace NSchema.Tests.Operations.Plan;

public sealed class PlanOperationTests
{
    private readonly IMigrationWorkflow _workflow = Substitute.For<IMigrationWorkflow>();
    private readonly IPlanFileWriter _planFile = Substitute.For<IPlanFileWriter>();
    private readonly ISqlGenerator _sqlGenerator = Substitute.For<ISqlGenerator>();

    private readonly DatabaseDiff _diff = new([new SchemaDiff("app", ChangeKind.Add)]);
    private readonly MigrationPlan _plan = new([], [], []);
    private readonly SqlPlan _sqlPlan = new([new SqlStatement("CREATE SCHEMA app")]);

    private PlanOperation BuildSut(ISqlGenerator? generator) => new(_workflow, _planFile, generator);
    private readonly PlanOperation _sut;

    public PlanOperationTests()
    {
        var planned = Result.Success(new PlannedMigration(_diff, _plan));
        _workflow.ComputePlan(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(planned);
        _workflow.ComputeTeardown(Arg.Any<CancellationToken>()).Returns(planned);
        _sqlGenerator.Generate(Arg.Any<MigrationPlan>()).Returns(_sqlPlan);

        _sut = BuildSut(_sqlGenerator);
    }

    private static PlanArguments Args(PlanTarget target = PlanTarget.Recorded, string[]? schemas = null, string? outFile = null) =>
        new() { Target = target, Schemas = schemas, OutFile = outFile };

    [Fact]
    public async Task Execute_Live_ComputesOnlinePlan_Required()
    {
        // Act
        await _sut.Execute(Args(PlanTarget.Live), TestContext.Current.CancellationToken);

        // Assert — an apply-bound plan must read the live database.
        await _workflow.Received(1).ComputePlan(SchemaSourceMode.Online, true, Arg.Any<string[]?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_Recorded_ComputesOfflinePlan_WithLiveFallback()
    {
        // Act
        await _sut.Execute(Args(), TestContext.Current.CancellationToken);

        // Assert — a preview prefers the recorded state but may fall back, so it is not required.
        await _workflow.Received(1).ComputePlan(SchemaSourceMode.Offline, false, Arg.Any<string[]?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_Teardown_ComputesTeardown_NotAPlan()
    {
        // Act
        await _sut.Execute(Args(PlanTarget.Teardown), TestContext.Current.CancellationToken);

        // Assert
        await _workflow.Received(1).ComputeTeardown(Arg.Any<CancellationToken>());
        await _workflow.DidNotReceive().ComputePlan(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ForwardsScopeToComputePlan()
    {
        // Act
        await _sut.Execute(Args(PlanTarget.Live, schemas: ["app", "legacy"]), TestContext.Current.CancellationToken);

        // Assert
        await _workflow.Received(1).ComputePlan(
            Arg.Any<SchemaSourceMode>(), Arg.Any<bool>(),
            Arg.Is<string[]?>(s => s != null && s.SequenceEqual(new[] { "app", "legacy" })), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_OnSuccess_ProducesDiffPlanAndGeneratedSql()
    {
        // Act
        var result = await _sut.Execute(Args(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Diff.ShouldBe(_diff);
        result.Value!.Plan.ShouldBe(_plan);
        result.Value!.Sql.ShouldBe(_sqlPlan);
        result.Value!.HasChanges.ShouldBeTrue();
    }

    [Fact]
    public async Task Execute_OnSuccess_WithOutFile_WritesTheEnvelope()
    {
        // Act
        await _sut.Execute(Args(outFile: "plan.nschema"), TestContext.Current.CancellationToken);

        // Assert — the saved envelope carries the diff, plan, and generated SQL.
        await _planFile.Received(1).Write(
            "plan.nschema",
            Arg.Is<PlanFileEnvelope>(e => e.Diff == _diff && e.Plan == _plan && e.Sql == _sqlPlan),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_OnSuccess_NoProvider_NoOutFile_WarnsThatThereIsNoSqlPreview()
    {
        // Arrange
        var sut = BuildSut(generator: null);

        // Act
        var result = await sut.Execute(Args(), TestContext.Current.CancellationToken);

        // Assert — no provider means no SQL, but a preview without SQL is still a (warned) success.
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Plan.ShouldBe(_plan);
        result.Value!.Sql.ShouldBeNull();
        result.Diagnostics.ShouldContain(d => d.Severity == DiagnosticSeverity.Warning && d.Message.Contains("Unable to generate SQL preview"));
    }

    [Fact]
    public async Task Execute_OnSuccess_NoProvider_WithOutFile_FailsBecauseSavingNeedsSql()
    {
        // Arrange
        var sut = BuildSut(generator: null);

        // Act
        var result = await sut.Execute(Args(outFile: "plan.nschema"), TestContext.Current.CancellationToken);

        // Assert — there is no SQL to persist, so saving fails and nothing is written.
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(d => d.Message.Contains("Saving a plan to a file requires a database provider"));
        await _planFile.DidNotReceive().Write(Arg.Any<string>(), Arg.Any<PlanFileEnvelope>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_OnPolicyFailure_CarriesTheDiffButNoPlanOrSql()
    {
        // Arrange — a diff policy fails, so the result is a failure that still carries the offending diff for display.
        _workflow.ComputePlan(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(Result.From(new PlannedMigration(_diff, _plan), [Diagnostic.Error("destructive", "drops table")]));

        // Act
        var result = await _sut.Execute(Args(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Value!.Diff.ShouldBe(_diff);
        result.Value!.Plan.ShouldBeNull();
        result.Value!.Sql.ShouldBeNull();
        result.Errors.ShouldContain(d => d.Message == "drops table");
        _sqlGenerator.DidNotReceive().Generate(Arg.Any<MigrationPlan>());
    }

    [Fact]
    public async Task Execute_WhenPlanningProducesNoValue_LeavesEverythingNull()
    {
        // Arrange — a fatal schema-policy error short-circuits before a diff is even produced.
        _workflow.ComputePlan(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<PlannedMigration>(Diagnostic.Error("schema", "bad schema")));

        // Act
        var result = await _sut.Execute(Args(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Value!.Diff.ShouldBeNull();
        result.Value!.Plan.ShouldBeNull();
        result.Value!.Sql.ShouldBeNull();
    }
}
