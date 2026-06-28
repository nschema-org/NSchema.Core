using NSchema.Diff.Model;
using NSchema.Diagnostics;
using NSchema.Operations;
using NSchema.Operations.Progress;
using NSchema.Operations.Services;
using NSchema.Plan;
using NSchema.Plan.Model;
using NSchema.Plan.Model.Schemas;
using NSchema.Policies;
using NSchema.Schema;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Schemas;
using NSchema.Schema.Model.Scripts;
using NSchema.Schema.Model.Tables;
using NSchema.State;

namespace NSchema.Tests.Operations.Services;

public sealed class MigrationWorkflowTests
{
    private readonly IMigrationPlanner _planner = Substitute.For<IMigrationPlanner>();
    private readonly IOperationReporter _reporter = Substitute.For<IOperationReporter>();
    private readonly IProgress<OperationProgress> _progress = Substitute.For<IProgress<OperationProgress>>();
    private readonly ICurrentSchemaProvider _currentProvider = Substitute.For<ICurrentSchemaProvider>();
    private readonly IDesiredSchemaProvider _desiredProvider = Substitute.For<IDesiredSchemaProvider>();
    private readonly ISchemaStateSerializer _stateSerializer = new SchemaStateSerializer();

    private MigrationWorkflow BuildSut(ISchemaStateStore? store = null) =>
        new(_planner, _reporter, _progress, _currentProvider, _desiredProvider, _stateSerializer, store);

    private static DesiredProject Project(DatabaseSchema schema) => new(schema, []);

    private readonly MigrationWorkflow _sut;

    public MigrationWorkflowTests()
    {
        _currentProvider
            .GetSchema(Arg.Any<SchemaSourceMode>(), Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new DatabaseSchema([]));

        _desiredProvider
            .GetProject(Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(Project(new DatabaseSchema([])));

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
    public async Task ValidateDesiredSchema_ReturnsSuccess_WhenNoPolicyErrors()
    {
        // Arrange
        var desired = new DatabaseSchema([new SchemaDefinition("app")]);
        _desiredProvider.GetProject(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(Project(desired));

        // Act
        var result = await _sut.Validate(TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task ValidateDesiredSchema_PolicyViolation_ReturnsFailure_WithoutReporting()
    {
        // Arrange
        _planner.Validate(Arg.Any<DatabaseSchema>()).Returns(new PolicyDiagnostics([Diagnostic.Error("P1", "msg")]));

        // Act
        var result = await _sut.Validate(TestContext.Current.CancellationToken);

        // Assert — the failure is carried back, not thrown; the workflow no longer renders it (the caller does).
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldBe("msg");
        _reporter.DidNotReceive().ReportDiagnostics(Arg.Any<PolicyDiagnostics>());
    }

    [Fact]
    public async Task ValidateDesiredSchema_NonErrorDiagnostics_AreCarriedInTheResult()
    {
        // Arrange
        _planner.Validate(Arg.Any<DatabaseSchema>())
            .Returns(new PolicyDiagnostics([new Diagnostic("P1", "info", DiagnosticSeverity.Info)]));

        // Act
        var result = await _sut.Validate(TestContext.Current.CancellationToken);

        // Assert — advisories ride along in a successful result rather than being reported here.
        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldBe("info");
        _reporter.DidNotReceive().ReportDiagnostics(Arg.Any<PolicyDiagnostics>());
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
    public async Task ComputePlan_ReturnsComputedPlan_WithoutRendering()
    {
        // Arrange
        var plan = new MigrationPlan([new CreateSchema("app")], [], []);
        var diff = new DatabaseDiff([]);
        _planner
            .Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<IReadOnlyList<Script>>())
            .Returns(new MigrationPlanResult(plan, diff, []));

        // Act
        var result = await _sut.ComputePlan(SchemaSourceMode.Offline, required: false, null, TestContext.Current.CancellationToken);

        // Assert — the result is returned for the caller to render; the workflow renders nothing itself.
        result.Plan.ShouldBe(plan);
        result.Diff.ShouldBe(diff);
        _reporter.DidNotReceive().ReportDiff(Arg.Any<DatabaseDiff>());
    }

    [Fact]
    public async Task Prepare_ForwardsSourceModeAndRequiredToCurrentProvider()
    {
        // Arrange

        // Act
        await _sut.ComputePlan(SchemaSourceMode.Online, required: true, null, TestContext.Current.CancellationToken);

        // Assert
        await _currentProvider.Received(1).GetSchema(
            SchemaSourceMode.Online, Arg.Any<string[]?>(), required: true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Prepare_PrefersOfflineSourceWithFallback()
    {
        // Arrange

        // Act
        await _sut.ComputePlan(SchemaSourceMode.Offline, required: false, null, TestContext.Current.CancellationToken);

        // Assert
        await _currentProvider.Received(1).GetSchema(
            SchemaSourceMode.Offline, Arg.Any<string[]?>(), required: false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Prepare_ReportsVerboseObjectCensusForBothSchemas()
    {
        // Arrange
        var desired = new DatabaseSchema([new SchemaDefinition("app", Tables:
            [new Table("users"), new Table("orders")])]);
        _desiredProvider.GetProject(Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(new DesiredProject(desired, [new Script("seed", "select 1", ScriptType.PostDeployment)], ["/app/users.sql", "/app/orders.sql"]));
        _currentProvider
            .GetSchema(SchemaSourceMode.Online, Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new DatabaseSchema([new SchemaDefinition("app")]));

        // Act
        await _sut.ComputePlan(SchemaSourceMode.Online, required: true, null, TestContext.Current.CancellationToken);

        // Assert — verbose census is transient narration, now emitted as Detail-level progress.
        _progress.Received().Report(OperationProgress.Detail("Read 2 DDL files:"));
        _progress.Received().Report(OperationProgress.Detail("/app/users.sql"));
        _progress.Received().Report(OperationProgress.Detail("/app/orders.sql"));
        _progress.Received().Report(OperationProgress.Detail("Desired schema: 1 schema, 2 tables, 1 deployment script."));
        _progress.Received().Report(OperationProgress.Detail("Current schema (online): 1 schema, 0 tables."));
    }

    [Fact]
    public async Task ComputePlan_PolicyViolation_ReturnsErrors_WithoutThrowingOrReporting()
    {
        // Arrange
        var errors = new[] { Diagnostic.Error("P1", "msg") };
        _planner.Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<IReadOnlyList<Script>>())
            .Returns(new MigrationPlanResult(null, null, errors));

        // Act — the failure is carried in the result, not thrown; the caller decides how to surface it.
        var result = await _sut.ComputePlan(SchemaSourceMode.Offline, required: false, null, TestContext.Current.CancellationToken);

        // Assert
        result.HasErrors.ShouldBeTrue();
        result.Diagnostics.Errors.ShouldHaveSingleItem().Message.ShouldBe("msg");
        _reporter.DidNotReceive().ReportDiagnostics(Arg.Any<PolicyDiagnostics>());
        _reporter.DidNotReceive().ReportDiff(Arg.Any<DatabaseDiff>());
    }

    [Fact]
    public async Task ComputePlan_DiffPolicyViolation_CarriesTheDiffAndErrors()
    {
        // Arrange: a diff policy (e.g. destructive-action on a dropped table) fails, so the result
        // carries errors but also the diff that triggered them — for the caller to render.
        var diff = new DatabaseDiff([]);
        var errors = new[] { Diagnostic.Error("destructive", "drops table") };
        _planner.Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<IReadOnlyList<Script>>())
            .Returns(new MigrationPlanResult(new MigrationPlan([], [], []), diff, errors));

        // Act
        var result = await _sut.ComputePlan(SchemaSourceMode.Offline, required: false, null, TestContext.Current.CancellationToken);

        // Assert
        result.HasErrors.ShouldBeTrue();
        result.Diff.ShouldBe(diff);
        _reporter.DidNotReceive().ReportDiff(Arg.Any<DatabaseDiff>());
    }

    [Fact]
    public async Task ComputePlan_CarriesNonErrorDiagnostics_WithoutReporting()
    {
        // Arrange
        var diagnostics = new[] { new Diagnostic("P1", "info", DiagnosticSeverity.Info) };
        var plan = new MigrationPlan([], [], []);
        _planner.Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<IReadOnlyList<Script>>())
            .Returns(new MigrationPlanResult(plan, new DatabaseDiff([]), diagnostics));

        // Act
        var result = await _sut.ComputePlan(SchemaSourceMode.Offline, required: false, null, TestContext.Current.CancellationToken);

        // Assert — advisories ride in the result, rendered by the caller, not reported here.
        result.Diagnostics.ShouldBe(diagnostics);
        _reporter.DidNotReceive().ReportDiagnostics(Arg.Any<PolicyDiagnostics>());
    }

    [Fact]
    public async Task Prepare_DerivesScopeFromDesiredSchema_WhenNoExplicitScope()
    {
        // Arrange
        var desired = new DatabaseSchema(
            [new SchemaDefinition("app"), new SchemaDefinition("admin")],
            DroppedSchemas: ["legacy"]);
        _desiredProvider.GetProject(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(Project(desired));
        string[]? capturedScope = null;
        _currentProvider
            .GetSchema(Arg.Any<SchemaSourceMode>(), Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(call => { capturedScope = call.ArgAt<string[]?>(1); return new DatabaseSchema([]); });

        // Act
        await _sut.ComputePlan(SchemaSourceMode.Offline, required: false, null, TestContext.Current.CancellationToken);

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
        _desiredProvider.GetProject(Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(call => { desiredScope = call.Arg<string[]?>(); return Project(new DatabaseSchema([])); });
        _currentProvider
            .GetSchema(Arg.Any<SchemaSourceMode>(), Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(call => { currentScope = call.ArgAt<string[]?>(1); return new DatabaseSchema([]); });
        // Act
        await _sut.ComputePlan(SchemaSourceMode.Offline, required: false, ["app", "legacy"], TestContext.Current.CancellationToken);

        // Assert
        desiredScope.ShouldBe(["app", "legacy"]);
        currentScope.ShouldBe(["app", "legacy"]);
    }

    [Fact]
    public async Task Prepare_PassesNullScopeToDesiredProvider_WhenNoExplicitScope()
    {
        // Arrange
        string[]? desiredScope = [];
        _desiredProvider.GetProject(Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(call => { desiredScope = call.Arg<string[]?>(); return Project(new DatabaseSchema([])); });

        // Act
        await _sut.ComputePlan(SchemaSourceMode.Offline, required: false, null, TestContext.Current.CancellationToken);

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
        await sut.ComputeTeardown(TestContext.Current.CancellationToken);

        // Assert
        await _currentProvider.Received(1).GetSchema(
            SchemaSourceMode.Offline, Arg.Any<string[]?>(), required: true, Arg.Any<CancellationToken>());
        _planner.Received(1).PlanTeardown(managed);
        await _desiredProvider.DidNotReceive().GetProject(Arg.Any<string[]?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlanDestroy_WithoutStore_TearsDownDesiredSchema()
    {
        // Arrange
        var managed = new DatabaseSchema([new SchemaDefinition("app")]);
        _desiredProvider.GetProject(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(Project(managed));
        var sut = BuildSut(store: null);

        // Act
        await sut.ComputeTeardown(TestContext.Current.CancellationToken);

        // Assert
        _planner.Received(1).PlanTeardown(managed);
        await _currentProvider.DidNotReceive().GetSchema(
            Arg.Any<SchemaSourceMode>(), Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ComputeTeardown_ReturnsTeardownPlan_WithoutRendering()
    {
        // Arrange
        var plan = new MigrationPlan([new DropSchema("app")], [], []);
        var diff = new DatabaseDiff([]);
        _planner.PlanTeardown(Arg.Any<DatabaseSchema>()).Returns(new MigrationPlanResult(plan, diff, []));

        // Act
        var result = await _sut.ComputeTeardown(TestContext.Current.CancellationToken);

        // Assert
        result.Plan.ShouldBe(plan);
        result.Diff.ShouldBe(diff);
        _reporter.DidNotReceive().ReportDiff(Arg.Any<DatabaseDiff>());
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
