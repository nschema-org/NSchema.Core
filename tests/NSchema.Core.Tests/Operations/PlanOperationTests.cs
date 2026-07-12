using NSchema.Operations.Workflow;
using NSchema.Diff.Domain.Models.Schemas;
using NSchema.Current;
using NSchema.Diff.Domain.Models;
using NSchema.Operations;
using NSchema.Plan.Domain.Models;
using NSchema.Plan.PlanFile;

namespace NSchema.Tests.Operations;

public sealed class PlanOperationTests
{
    private readonly IMigrationWorkflow _workflow = Substitute.For<IMigrationWorkflow>();
    private readonly IPlanFileWriter _planFile = Substitute.For<IPlanFileWriter>();

    private readonly MigrationPlan _plan = new(
        new DatabaseDiff([new SchemaDiff("app", ChangeKind.Add)]),
        [new SqlStatement("CREATE SCHEMA app")]);

    private readonly PlanOperation _sut;

    public PlanOperationTests()
    {
        var planned = Result.Success(_plan);
        _workflow.ComputePlan(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(planned);
        _workflow.ComputeTeardown(Arg.Any<CancellationToken>()).Returns(planned);

        _sut = new PlanOperation(_workflow, _planFile);
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
    public async Task Execute_OnSuccess_ProducesThePlan()
    {
        // Act
        var result = await _sut.Execute(Args(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Plan.ShouldBe(_plan);
        result.Value!.HasChanges.ShouldBeTrue();
    }

    [Fact]
    public async Task Execute_OnSuccess_WithOutFile_WritesTheEnvelope()
    {
        // Act
        await _sut.Execute(Args(outFile: "plan.nschema"), TestContext.Current.CancellationToken);

        // Assert
        await _planFile.Received(1).Write(
            "plan.nschema",
            Arg.Is<PlanFileEnvelope>(e => e.Plan == _plan),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_OnPolicyFailure_StillCarriesTheCompletePlan()
    {
        // Arrange — a policy blocks the plan; the failure still carries the full artifact so the offending
        // change (and its SQL) stays visible.
        _workflow.ComputePlan(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(Result.From(_plan, [Diagnostic.Error("destructive", "drops table")]));

        // Act
        var result = await _sut.Execute(Args(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Value!.Plan.ShouldBe(_plan);
        result.Errors.ShouldContain(d => d.Message == "drops table");
    }

    [Fact]
    public async Task Execute_OnPolicyFailure_WithOutFile_StillWritesThePlan()
    {
        // Arrange — the file is a review artifact, not a bypass: apply enforces the policies again against the
        // carried diff, so writing a blocked plan is safe and the failing result reports the block.
        _workflow.ComputePlan(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(Result.From(_plan, [Diagnostic.Error("destructive", "drops table")]));

        // Act
        var result = await _sut.Execute(Args(outFile: "plan.nschema"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        await _planFile.Received(1).Write("plan.nschema", Arg.Is<PlanFileEnvelope>(e => e.Plan == _plan), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenPlanningProducesNoValue_CarriesNoPlan()
    {
        // Arrange — planning could not run at all (e.g. no provider registered).
        _workflow.ComputePlan(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<MigrationPlan>(Diagnostic.Error("plan", "no provider")));

        // Act
        var result = await _sut.Execute(Args(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Value!.Plan.ShouldBeNull();
        result.Value!.HasChanges.ShouldBeFalse();
    }
}
