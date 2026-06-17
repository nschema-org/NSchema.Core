using NSchema.Diff.Model;
using NSchema.Operations;
using NSchema.Operations.Services;
using NSchema.Plan;
using NSchema.Plan.Model;
using NSchema.Policies;
using NSchema.Schema;
using NSchema.Schema.Model;
using NSchema.Scripts.Model;
using NSchema.State;

namespace NSchema.Tests.Operations.Services;

public sealed class MigrationWorkflowTests
{
    private readonly IMigrationPlanner _planner = Substitute.For<IMigrationPlanner>();
    private readonly IOperationReporter _reporter = Substitute.For<IOperationReporter>();
    private readonly ICurrentSchemaProvider _currentProvider = Substitute.For<ICurrentSchemaProvider>();
    private readonly IDesiredSchemaProvider _desiredProvider = Substitute.For<IDesiredSchemaProvider>();
    private readonly ISchemaStateSerializer _stateSerializer = new SchemaStateSerializer();

    private MigrationWorkflow BuildSut(ISchemaStateStore? store = null) =>
        new(_planner, [], Helpers.TestReporters.ResolverFor(_reporter), _currentProvider, _desiredProvider, _stateSerializer, store);

    private readonly MigrationWorkflow _sut;

    public MigrationWorkflowTests()
    {
        _currentProvider
            .GetSchema(Arg.Any<SchemaSourceMode>(), Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new DatabaseSchema([]));

        _desiredProvider
            .GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(new DatabaseSchema([]));

        _planner.Validate(Arg.Any<DatabaseSchema>()).Returns(new PolicyDiagnostics());

        _planner
            .Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<IReadOnlyList<Script>>())
            .Returns(new MigrationPlanResult(new MigrationPlan([], [], []), new DatabaseDiff([]), []));

        _planner
            .PlanTeardown(Arg.Any<DatabaseSchema>())
            .Returns(new MigrationPlanResult(new MigrationPlan([], [], []), new DatabaseDiff([]), []));

        _sut = BuildSut();
    }

    [Fact]
    public async Task ValidateDesiredSchema_ReturnsLoadedSchema_WhenNoPolicyErrors()
    {
        // Arrange
        var desired = new DatabaseSchema([new SchemaDefinition("app")]);
        _desiredProvider.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(desired);

        // Act
        var result = await _sut.Validate(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe(desired);
    }

    [Fact]
    public async Task ValidateDesiredSchema_PolicyViolation_ThrowsWithoutReporting()
    {
        // Arrange
        _planner.Validate(Arg.Any<DatabaseSchema>()).Returns(new PolicyDiagnostics([PolicyDiagnostic.Error("P1", "msg")]));

        // Act
        var act = () => _sut.Validate(TestContext.Current.CancellationToken);

        // Assert
        await act.ShouldThrowAsync<PolicyViolationException>();
        _reporter.DidNotReceive().ReportDiagnostics(Arg.Any<PolicyDiagnostics>());
    }

    [Fact]
    public async Task ValidateDesiredSchema_NonErrorDiagnostics_AreReported()
    {
        // Arrange
        _planner.Validate(Arg.Any<DatabaseSchema>())
            .Returns(new PolicyDiagnostics([new PolicyDiagnostic("P1", "info", PolicyDiagnosticSeverity.Info)]));

        // Act
        await _sut.Validate(TestContext.Current.CancellationToken);

        // Assert
        _reporter.Received(1).ReportDiagnostics(Arg.Any<PolicyDiagnostics>());
    }

    [Fact]
    public async Task ValidateDesiredSchema_DoesNotContactCurrentProvider()
    {
        // Act
        await _sut.Validate(TestContext.Current.CancellationToken);

        // Assert
        await _currentProvider.DidNotReceive().GetSchema(
            Arg.Any<SchemaSourceMode>(), Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Prepare_ReturnsComputedPlan_AndReportsItsDiff()
    {
        // Arrange
        var plan = new MigrationPlan([new CreateSchema("app")], [], []);
        var diff = new DatabaseDiff([]);
        _planner
            .Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<IReadOnlyList<Script>>())
            .Returns(new MigrationPlanResult(plan, diff, []));

        // Act
        var result = await _sut.Plan(SchemaSourceMode.Offline, required: false, null, TestContext.Current.CancellationToken);

        // Assert
        result.Plan.ShouldBe(plan);
        result.Diff.ShouldBe(diff);
        _reporter.Received(1).ReportDiff(diff);
    }

    [Fact]
    public async Task Prepare_ForwardsSourceModeAndRequiredToCurrentProvider()
    {
        // Arrange

        // Act
        await _sut.Plan(SchemaSourceMode.Online, required: true, null, TestContext.Current.CancellationToken);

        // Assert
        await _currentProvider.Received(1).GetSchema(
            SchemaSourceMode.Online, Arg.Any<string[]?>(), required: true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Prepare_PrefersOfflineSourceWithFallback()
    {
        // Arrange

        // Act
        await _sut.Plan(SchemaSourceMode.Offline, required: false, null, TestContext.Current.CancellationToken);

        // Assert
        await _currentProvider.Received(1).GetSchema(
            SchemaSourceMode.Offline, Arg.Any<string[]?>(), required: false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Prepare_PolicyViolation_ThrowsWithoutReporting()
    {
        // Arrange
        var errors = new[] { PolicyDiagnostic.Error("P1", "msg") };
        _planner.Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<IReadOnlyList<Script>>())
            .Returns(new MigrationPlanResult(null, null, errors));

        // Act
        var act = () => _sut.Plan(SchemaSourceMode.Offline, required: false, null, TestContext.Current.CancellationToken);

        // Assert
        await act.ShouldThrowAsync<PolicyViolationException>();
        _reporter.DidNotReceive().ReportDiagnostics(Arg.Any<PolicyDiagnostics>());
        _reporter.DidNotReceive().ReportDiff(Arg.Any<DatabaseDiff>());
    }

    [Fact]
    public async Task Prepare_DiffPolicyViolation_ReportsDiffBeforeThrowing()
    {
        // Arrange: a diff policy (e.g. destructive-action on a dropped table) fails, so the result
        // carries errors but also the diff that triggered them. The user must see that diff.
        var diff = new DatabaseDiff([]);
        var errors = new[] { PolicyDiagnostic.Error("destructive", "drops table") };
        _planner.Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<IReadOnlyList<Script>>())
            .Returns(new MigrationPlanResult(new MigrationPlan([], [], []), diff, errors));

        // Act
        var act = () => _sut.Plan(SchemaSourceMode.Offline, required: false, null, TestContext.Current.CancellationToken);

        // Assert
        await act.ShouldThrowAsync<PolicyViolationException>();
        _reporter.Received(1).ReportDiff(diff);
    }

    [Fact]
    public async Task Prepare_NonErrorDiagnostics_ReportedAfterDiff()
    {
        // Arrange
        var diagnostics = new[] { new PolicyDiagnostic("P1", "info", PolicyDiagnosticSeverity.Info) };
        var plan = new MigrationPlan([], [], []);
        _planner.Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<IReadOnlyList<Script>>())
            .Returns(new MigrationPlanResult(plan, new DatabaseDiff([]), diagnostics));

        var callOrder = new List<string>();
        _reporter.When(r => r.ReportDiff(Arg.Any<DatabaseDiff>())).Do(_ => callOrder.Add("diff"));
        _reporter.When(r => r.ReportDiagnostics(Arg.Any<PolicyDiagnostics>())).Do(_ => callOrder.Add("diagnostics"));

        // Act
        await _sut.Plan(SchemaSourceMode.Offline, required: false, null, TestContext.Current.CancellationToken);

        // Assert
        _reporter.Received(1).ReportDiagnostics(Arg.Is<PolicyDiagnostics>(d => d.SequenceEqual(diagnostics)));
        callOrder.ShouldBe(["diff", "diagnostics"]);
    }

    [Fact]
    public async Task Prepare_DerivesScopeFromDesiredSchema_WhenNoExplicitScope()
    {
        // Arrange
        var desired = new DatabaseSchema(
            [new SchemaDefinition("app"), new SchemaDefinition("admin")],
            DroppedSchemas: ["legacy"]);
        _desiredProvider.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(desired);
        string[]? capturedScope = null;
        _currentProvider
            .GetSchema(Arg.Any<SchemaSourceMode>(), Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(call => { capturedScope = call.ArgAt<string[]?>(1); return new DatabaseSchema([]); });

        // Act
        await _sut.Plan(SchemaSourceMode.Offline, required: false, null, TestContext.Current.CancellationToken);

        // Assert
        capturedScope.ShouldNotBeNull();
        capturedScope!.ShouldBe(["app", "admin", "legacy"], ignoreOrder: true);
    }

    [Fact]
    public async Task Prepare_PassesExplicitScopeToDesiredAndCurrentProviders()
    {
        // Arrange
        string[]? desiredScope = null;
        string[]? currentScope = null;
        _desiredProvider.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(call => { desiredScope = call.Arg<string[]?>(); return new DatabaseSchema([]); });
        _currentProvider
            .GetSchema(Arg.Any<SchemaSourceMode>(), Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(call => { currentScope = call.ArgAt<string[]?>(1); return new DatabaseSchema([]); });
        // Act
        await _sut.Plan(SchemaSourceMode.Offline, required: false, ["app", "legacy"], TestContext.Current.CancellationToken);

        // Assert
        desiredScope.ShouldBe(["app", "legacy"]);
        currentScope.ShouldBe(["app", "legacy"]);
    }

    [Fact]
    public async Task Prepare_PassesNullScopeToDesiredProvider_WhenNoExplicitScope()
    {
        // Arrange
        string[]? desiredScope = [];
        _desiredProvider.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(call => { desiredScope = call.Arg<string[]?>(); return new DatabaseSchema([]); });

        // Act
        await _sut.Plan(SchemaSourceMode.Offline, required: false, null, TestContext.Current.CancellationToken);

        // Assert
        desiredScope.ShouldBeNull();
    }

    [Fact]
    public async Task PlanDestroy_WithStore_TearsDownOfflineSchema()
    {
        // Arrange
        var managed = new DatabaseSchema([new SchemaDefinition("app")]);
        _currentProvider
            .GetSchema(SchemaSourceMode.Offline, Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(managed);
        var sut = BuildSut(Substitute.For<ISchemaStateStore>());

        // Act
        await sut.PlanDestroy(TestContext.Current.CancellationToken);

        // Assert
        await _currentProvider.Received(1).GetSchema(
            SchemaSourceMode.Offline, Arg.Any<string[]?>(), required: true, Arg.Any<CancellationToken>());
        _planner.Received(1).PlanTeardown(managed);
        await _desiredProvider.DidNotReceive().GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlanDestroy_WithoutStore_TearsDownDesiredSchema()
    {
        // Arrange
        var managed = new DatabaseSchema([new SchemaDefinition("app")]);
        _desiredProvider.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(managed);
        var sut = BuildSut(store: null);

        // Act
        await sut.PlanDestroy(TestContext.Current.CancellationToken);

        // Assert
        _planner.Received(1).PlanTeardown(managed);
        await _currentProvider.DidNotReceive().GetSchema(
            Arg.Any<SchemaSourceMode>(), Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlanDestroy_ReturnsTeardownPlan_AndReportsItsDiff()
    {
        // Arrange
        var plan = new MigrationPlan([new DropSchema("app")], [], []);
        var diff = new DatabaseDiff([]);
        _planner.PlanTeardown(Arg.Any<DatabaseSchema>()).Returns(new MigrationPlanResult(plan, diff, []));

        // Act
        var result = await _sut.PlanDestroy(TestContext.Current.CancellationToken);

        // Assert
        result.Plan.ShouldBe(plan);
        result.Diff.ShouldBe(diff);
        _reporter.Received(1).ReportDiff(diff);
    }

    [Fact]
    public async Task Refresh_WritesLiveSchemaToStore_Unscoped()
    {
        // Arrange
        var schema = new DatabaseSchema([new SchemaDefinition("app")]);
        var store = Substitute.For<ISchemaStateStore>();
        _currentProvider
            .GetSchema(SchemaSourceMode.Online, Arg.Any<string[]?>(), required: true, Arg.Any<CancellationToken>())
            .Returns(schema);
        var sut = BuildSut(store);

        var expected = _stateSerializer.Serialize(schema).ToArray();

        // Act
        await sut.Refresh(RefreshMode.Required, TestContext.Current.CancellationToken);

        // Assert
        await _currentProvider.Received(1).GetSchema(
            SchemaSourceMode.Online, Arg.Is<string[]?>(names => names == null), required: true, Arg.Any<CancellationToken>());
        await store.Received(1).Write(
            Arg.Is<ReadOnlyMemory<byte>>(m => m.ToArray().SequenceEqual(expected)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_Required_NoStore_Throws()
    {
        // Arrange
        var sut = BuildSut(store: null);

        // Act
        var act = () => sut.Refresh(RefreshMode.Required, TestContext.Current.CancellationToken);

        // Assert
        await act.ShouldThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Refresh_Optional_NoStore_IsNoOp()
    {
        // Arrange
        var sut = BuildSut(store: null);

        // Act / Assert: no store, but Optional — completes without contacting the live database or throwing.
        await sut.Refresh(RefreshMode.Optional, TestContext.Current.CancellationToken);

        await _currentProvider.DidNotReceive().GetSchema(
            SchemaSourceMode.Online, Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }
}
