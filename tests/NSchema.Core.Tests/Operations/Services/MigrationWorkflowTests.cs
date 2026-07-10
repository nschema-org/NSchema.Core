using NSchema.Diagnostics;
using NSchema.Diff.Model;
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
using NSchema.Sql.Model;
using NSchema.State;
using NSchema.State.Model;
using NSchema.State.Storage;

namespace NSchema.Tests.Operations.Services;

public sealed class MigrationWorkflowTests
{
    private readonly IMigrationPlanner _planner = Substitute.For<IMigrationPlanner>();
    private readonly IProgress<OperationProgress> _progress = Substitute.For<IProgress<OperationProgress>>();
    private readonly ICurrentSchemaProvider _currentProvider = Substitute.For<ICurrentSchemaProvider>();
    private readonly IDesiredSchemaProvider _desiredProvider = Substitute.For<IDesiredSchemaProvider>();
    private readonly ISchemaStateSerializer _stateSerializer = new SchemaStateSerializer();

    private MigrationWorkflow BuildSut(ISchemaStateStore? store = null) =>
        new(_planner, _progress, _currentProvider, _desiredProvider, new SchemaStateManager(_stateSerializer, store));

    private static DesiredProjectResult Project(DatabaseSchema schema) => new(new DesiredProject(schema, [], []), [], []);

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
            .Plan(Arg.Any<CurrentState>(), Arg.Any<DesiredProject>())
            .Returns(Result.Success(new PlannedMigration(new DatabaseDiff([]), new MigrationPlan([], [], []))));

        _planner
            .PlanTeardown(Arg.Any<DatabaseSchema>())
            .Returns(Result.Success(new PlannedMigration(new DatabaseDiff([]), new MigrationPlan([], [], []))));

        _sut = BuildSut();
    }

    [Fact]
    public async Task ValidateDesiredSchema_ReturnsNoFindings_WhenNoPolicyErrors()
    {
        // Arrange
        var desired = new DatabaseSchema([new SchemaDefinition("app")]);
        _desiredProvider.GetProject(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(Project(desired));

        // Act
        var findings = await _sut.Validate(TestContext.Current.CancellationToken);

        // Assert — the findings are data, not a pass/fail verdict; a clean schema yields none.
        findings.HasErrors.ShouldBeFalse();
        findings.ShouldBeEmpty();
    }

    [Fact]
    public async Task ValidateDesiredSchema_PolicyViolation_ReturnsErrorFindings_WithoutReporting()
    {
        // Arrange
        _planner.Validate(Arg.Any<DatabaseSchema>()).Returns(new PolicyDiagnostics([Diagnostic.Error("P1", "msg")]));

        // Act
        var findings = await _sut.Validate(TestContext.Current.CancellationToken);

        // Assert — the findings are carried back as data, not thrown; the workflow no longer renders them (the caller does).
        findings.HasErrors.ShouldBeTrue();
        findings.Errors.ShouldHaveSingleItem().Message.ShouldBe("msg");
    }

    [Fact]
    public async Task ValidateDesiredSchema_NonErrorDiagnostics_AreCarriedInTheFindings()
    {
        // Arrange
        _planner.Validate(Arg.Any<DatabaseSchema>())
            .Returns(new PolicyDiagnostics([new Diagnostic("P1", "info", DiagnosticSeverity.Info)]));

        // Act
        var findings = await _sut.Validate(TestContext.Current.CancellationToken);

        // Assert — advisories ride along in the findings rather than being reported here.
        findings.HasErrors.ShouldBeFalse();
        findings.ShouldHaveSingleItem().Message.ShouldBe("info");
    }

    [Fact]
    public async Task ValidateDesiredSchema_ProjectDiagnostics_AreCarriedInTheFindings()
    {
        // Arrange — findings raised while reading the DDL (e.g. deprecated syntax) arrive on the read result.
        var project = new DesiredProjectResult(new DesiredProject(new DatabaseSchema([]), [], []), [],
            [Diagnostic.Warning("deprecations", "old form")]);
        _desiredProvider.GetProject(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(project);

        // Act
        var findings = await _sut.Validate(TestContext.Current.CancellationToken);

        // Assert
        findings.HasErrors.ShouldBeFalse();
        findings.ShouldHaveSingleItem().Source.ShouldBe("deprecations");
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
            .Plan(Arg.Any<CurrentState>(), Arg.Any<DesiredProject>())
            .Returns(Result.Success(new PlannedMigration(diff, plan)));

        // Act
        var result = await _sut.ComputePlan(SchemaSourceMode.Offline, required: false, null, TestContext.Current.CancellationToken);

        // Assert — the result is returned for the caller to render; the workflow renders nothing itself.
        result.Value!.Plan.ShouldBe(plan);
        result.Value!.Diff.ShouldBe(diff);
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
            .Returns(new DesiredProjectResult(
                new DesiredProject(desired, [new Script("seed", "select 1", ScriptType.PostDeployment)], []),
                ["/app/users.sql", "/app/orders.sql"], []));
        _currentProvider
            .GetSchema(SchemaSourceMode.Online, Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new DatabaseSchema([new SchemaDefinition("app")]));

        // Act
        await _sut.ComputePlan(SchemaSourceMode.Online, required: true, null, TestContext.Current.CancellationToken);

        // Assert — verbose census is transient narration, now emitted as Detail-level progress.
        _progress.Received().Report(OperationProgress.Detail("Read 2 DDL files:"));
        _progress.Received().Report(OperationProgress.Detail("/app/users.sql"));
        _progress.Received().Report(OperationProgress.Detail("/app/orders.sql"));
        _progress.Received().Report(OperationProgress.Detail("Desired schema: 1 schema, 2 tables, 1 deployment script, 0 data migrations."));
        _progress.Received().Report(OperationProgress.Detail("Current schema (online): 1 schema, 0 tables."));
    }

    [Fact]
    public async Task ComputePlan_ReadDiagnostics_ArePrependedToThePlannerResult()
    {
        // Arrange — the pure planner never sees read provenance; this shell merges it into the outcome.
        var project = new DesiredProjectResult(new DesiredProject(new DatabaseSchema([]), [], []), [],
            [Diagnostic.Warning("deprecations", "old form")]);
        _desiredProvider.GetProject(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(project);
        _planner.Plan(Arg.Any<CurrentState>(), Arg.Any<DesiredProject>())
            .Returns(Result.From(new PlannedMigration(new DatabaseDiff([]), new MigrationPlan([], [], [])),
                [Diagnostic.Warning("data-hazards", "hazard")]));

        // Act
        var result = await _sut.ComputePlan(SchemaSourceMode.Offline, required: false, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.Select(d => d.Source).ShouldBe(["deprecations", "data-hazards"]);
    }

    [Fact]
    public async Task ComputePlan_ReadDiagnostics_AreCarriedOnAPlannerFailureToo()
    {
        // Arrange
        var project = new DesiredProjectResult(new DesiredProject(new DatabaseSchema([]), [], []), [],
            [Diagnostic.Warning("deprecations", "old form")]);
        _desiredProvider.GetProject(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(project);
        _planner.Plan(Arg.Any<CurrentState>(), Arg.Any<DesiredProject>())
            .Returns(Result.Failure<PlannedMigration>([Diagnostic.Error("P1", "blocked")]));

        // Act
        var result = await _sut.ComputePlan(SchemaSourceMode.Offline, required: false, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Diagnostics.Select(d => d.Source).ShouldBe(["deprecations", "P1"]);
    }

    private static Script SeedScript(RunCondition condition = RunCondition.Once) =>
        new("seed", "SELECT 1", ScriptType.PostDeployment) { RunCondition = condition };

    /// <summary>Builds a workflow whose store records <paramref name="executions"/> and whose DDL declares <paramref name="project"/>.</summary>
    private MigrationWorkflow SutWithState(DesiredProject project, params ScriptRecord[] executions)
    {
        var store = Substitute.For<ISchemaStateStore>();
        store.Read(Arg.Any<CancellationToken>())
            .Returns(_stateSerializer.Serialize(new SchemaState(new DatabaseSchema([]), executions)));
        _desiredProvider.GetProject(Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(new DesiredProjectResult(project, [], []));
        return BuildSut(store);
    }

    [Fact]
    public async Task ComputePlan_TranslatesRecordedExecutionsIntoTheCurrentState()
    {
        // Arrange — the planner's "what I have" input is the schema plus the recorded executions, translated
        // to the planning model at this boundary (the planner never sees state records).
        var sut = SutWithState(new DesiredProject(new DatabaseSchema([]), [SeedScript()], []),
            new ScriptRecord("seed", "abc", DateTimeOffset.UnixEpoch));

        // Act
        await sut.ComputePlan(SchemaSourceMode.Online, required: true, null, TestContext.Current.CancellationToken);

        // Assert
        _planner.Received(1).Plan(
            Arg.Is<CurrentState>(c => c.ExecutedScripts.Count == 1 && c.ExecutedScripts[0] == new ScriptHash("seed", "abc")),
            Arg.Any<DesiredProject>());
    }

    [Fact]
    public async Task ComputePlan_UnreadableState_IsAFailure_WithoutPlanning()
    {
        // Arrange — an unreadable ledger must fail the plan rather than read as empty: an empty ledger
        // would re-plan every recorded run-once script.
        var store = Substitute.For<ISchemaStateStore>();
        store.Read(Arg.Any<CancellationToken>()).Returns((ReadOnlyMemory<byte>?)"not a state payload"u8.ToArray());
        var sut = BuildSut(store);

        // Act
        var result = await sut.ComputePlan(SchemaSourceMode.Online, required: true, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Source.ShouldBe("state");
        _planner.DidNotReceive().Plan(Arg.Any<CurrentState>(), Arg.Any<DesiredProject>());
    }

    [Fact]
    public async Task ComputePlan_NoStoreWithRunOnceScripts_Warns()
    {
        // Arrange — a run-once script but nowhere to record executions.
        var project = new DesiredProjectResult(new DesiredProject(new DatabaseSchema([]),
            [new Script("seed", "SELECT 1", ScriptType.PostDeployment) { RunCondition = RunCondition.Once }], []), [], []);
        _desiredProvider.GetProject(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(project);

        // Act
        var result = await _sut.ComputePlan(SchemaSourceMode.Online, required: true, null, TestContext.Current.CancellationToken);

        // Assert
        var diagnostic = result.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Source.ShouldBe("run-once");
        diagnostic.Message.ShouldContain("require a state store");
    }

    [Fact]
    public async Task Refresh_RecordsExecutedScripts()
    {
        // Arrange
        var store = Substitute.For<ISchemaStateStore>();
        ReadOnlyMemory<byte>? written = null;
        await store.Write(Arg.Do<ReadOnlyMemory<byte>>(m => written = m), Arg.Any<CancellationToken>());
        var sut = BuildSut(store);

        // Act
        await sut.Refresh(new SqlPlan([]) { Scripts = [new ScriptHash("seed", "abc")] }, TestContext.Current.CancellationToken);

        // Assert
        var execution = _stateSerializer.Deserialize(written!.Value).Scripts.ShouldHaveSingleItem();
        execution.Name.ShouldBe("seed");
        execution.Hash.ShouldBe("abc");
    }

    [Fact]
    public async Task Refresh_PreservesTheExistingLedger()
    {
        // Arrange — the ledger is the one part of state a capture cannot rebuild, so it must carry over.
        var existing = new ScriptRecord("api-login", "hash", DateTimeOffset.UnixEpoch);
        var store = Substitute.For<ISchemaStateStore>();
        store.Read(Arg.Any<CancellationToken>())
            .Returns(_stateSerializer.Serialize(new SchemaState(new DatabaseSchema([]), [existing])));
        ReadOnlyMemory<byte>? written = null;
        await store.Write(Arg.Do<ReadOnlyMemory<byte>>(m => written = m), Arg.Any<CancellationToken>());
        var sut = BuildSut(store);

        // Act — a plain refresh, nothing newly executed.
        await sut.Refresh(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        _stateSerializer.Deserialize(written!.Value).Scripts.ShouldHaveSingleItem().ShouldBe(existing);
    }

    [Fact]
    public async Task Refresh_ReRecordingAScript_ReplacesItsEntryByName()
    {
        // Arrange
        var existing = new ScriptRecord("seed", "old-hash", DateTimeOffset.UnixEpoch);
        var store = Substitute.For<ISchemaStateStore>();
        store.Read(Arg.Any<CancellationToken>())
            .Returns(_stateSerializer.Serialize(new SchemaState(new DatabaseSchema([]), [existing])));
        ReadOnlyMemory<byte>? written = null;
        await store.Write(Arg.Do<ReadOnlyMemory<byte>>(m => written = m), Arg.Any<CancellationToken>());
        var sut = BuildSut(store);

        // Act
        await sut.Refresh(new SqlPlan([]) { Scripts = [new ScriptHash("seed", "new-hash")] }, TestContext.Current.CancellationToken);

        // Assert
        _stateSerializer.Deserialize(written!.Value).Scripts.ShouldHaveSingleItem().Hash.ShouldBe("new-hash");
    }

    [Fact]
    public async Task Refresh_CorruptExistingState_StillCaptures_WithAnEmptyLedger()
    {
        // Arrange — refresh is the recovery path for corrupt state, so it must not fail on the old payload.
        var store = Substitute.For<ISchemaStateStore>();
        store.Read(Arg.Any<CancellationToken>()).Returns((ReadOnlyMemory<byte>?)new byte[] { 0x7b });
        ReadOnlyMemory<byte>? written = null;
        await store.Write(Arg.Do<ReadOnlyMemory<byte>>(m => written = m), Arg.Any<CancellationToken>());
        var sut = BuildSut(store);

        // Act
        var capture = await sut.Refresh(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        capture.ShouldNotBeNull();
        _stateSerializer.Deserialize(written!.Value).Scripts.ShouldBeEmpty();
    }

    [Fact]
    public async Task ComputePlan_PolicyViolation_ReturnsErrors_WithoutThrowingOrReporting()
    {
        // Arrange
        var errors = new[] { Diagnostic.Error("P1", "msg") };
        _planner.Plan(Arg.Any<CurrentState>(), Arg.Any<DesiredProject>())
            .Returns(Result.Failure<PlannedMigration>(errors));

        // Act — the failure is carried in the result, not thrown; the caller decides how to surface it.
        var result = await _sut.ComputePlan(SchemaSourceMode.Offline, required: false, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldBe("msg");
    }

    [Fact]
    public async Task ComputePlan_DiffPolicyViolation_CarriesTheDiffAndErrors()
    {
        // Arrange: a diff policy (e.g. destructive-action on a dropped table) fails, so the result
        // carries errors but also the diff that triggered them — for the caller to render.
        var diff = new DatabaseDiff([]);
        var errors = new[] { Diagnostic.Error("destructive", "drops table") };
        _planner.Plan(Arg.Any<CurrentState>(), Arg.Any<DesiredProject>())
            .Returns(Result.From(new PlannedMigration(diff, new MigrationPlan([], [], [])), errors));

        // Act
        var result = await _sut.ComputePlan(SchemaSourceMode.Offline, required: false, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Value!.Diff.ShouldBe(diff);
    }

    [Fact]
    public async Task ComputePlan_CarriesNonErrorDiagnostics_WithoutReporting()
    {
        // Arrange
        var diagnostics = new[] { new Diagnostic("P1", "info", DiagnosticSeverity.Info) };
        var plan = new MigrationPlan([], [], []);
        _planner.Plan(Arg.Any<CurrentState>(), Arg.Any<DesiredProject>())
            .Returns(Result.From(new PlannedMigration(new DatabaseDiff([]), plan), diagnostics));

        // Act
        var result = await _sut.ComputePlan(SchemaSourceMode.Offline, required: false, null, TestContext.Current.CancellationToken);

        // Assert — advisories ride in the result, rendered by the caller, not reported here.
        result.Diagnostics.ShouldBe(diagnostics);
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
        _planner.PlanTeardown(Arg.Any<DatabaseSchema>()).Returns(Result.Success(new PlannedMigration(diff, plan)));

        // Act
        var result = await _sut.ComputeTeardown(TestContext.Current.CancellationToken);

        // Assert
        result.Value!.Plan.ShouldBe(plan);
        result.Value!.Diff.ShouldBe(diff);
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

        var expected = _stateSerializer.Serialize(new SchemaState(schema)).ToArray();

        // Act
        var capture = await sut.Refresh(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        capture.ShouldNotBeNull();
        await _currentProvider.Received(1).GetSchema(
            SchemaSourceMode.Online, Arg.Is<string[]?>(names => names == null), required: true, Arg.Any<CancellationToken>());
        await store.Received(1).Write(
            Arg.Is<ReadOnlyMemory<byte>>(m => m.ToArray().SequenceEqual(expected)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_NoStore_ReturnsNull_WithoutContactingTheDatabase()
    {
        // Arrange
        var sut = BuildSut(store: null);

        // Act — no store, so nothing is captured; whether that is an error is the caller's call, not the workflow's.
        var capture = await sut.Refresh(cancellationToken: TestContext.Current.CancellationToken);

        // Assert: returns null (never throws) and does not touch the live database.
        capture.ShouldBeNull();
        await _currentProvider.DidNotReceive().GetSchema(
            SchemaSourceMode.Online, Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }
}
