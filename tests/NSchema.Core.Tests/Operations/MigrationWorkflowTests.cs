using NSchema.Current;
using NSchema.Current.Domain.Models;
using NSchema.Current.Storage;
using NSchema.Current.Storage.Backends;
using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Models;
using NSchema.Operations.Progress;
using NSchema.Operations.Workflow;
using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Models;
using NSchema.Policies;
using NSchema.Project;
using NSchema.Project.Domain;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Schemas;
using NSchema.Project.Domain.Models.Scripts;
using NSchema.Project.Domain.Models.Tables;

namespace NSchema.Tests.Operations;

public sealed class MigrationWorkflowTests
{
    private readonly IMigrationPlanner _planner = Substitute.For<IMigrationPlanner>();
    private readonly IProgress<OperationProgress> _progress = Substitute.For<IProgress<OperationProgress>>();
    private readonly ICurrentSchemaProvider _currentProvider = Substitute.For<ICurrentSchemaProvider>();
    private readonly IProjectProvider _desiredProvider = Substitute.For<IProjectProvider>();
    private readonly ISchemaStateSerializer _stateSerializer = new SchemaStateSerializer();

    private MigrationWorkflow BuildSut(ISchemaStateStore? store) =>
        new(_planner, _progress, _currentProvider, _desiredProvider, new SchemaStateManager(_stateSerializer, store));

    /// <summary>Planning requires a store, so the default fixture carries an empty in-memory one.</summary>
    private MigrationWorkflow BuildSut() => BuildSut(new EphemeralStateStore());

    private static Result<ProjectDefinition> ProjectDefinition(DatabaseSchema schema) => Result.Success(new ProjectDefinition(schema, []));

    private static MigrationPlan EmptyPlan() => new(new DatabaseDiff([]), []);

    /// <summary>An applied plan carrying one run-once script, so the capture has an execution to record.</summary>
    private static MigrationPlan AppliedPlan(string name, string sql) => new(
        new DatabaseDiff([]) { Scripts = [new Script(name, sql, new DeploymentEvent(DeploymentPhase.Post)) { RunCondition = RunCondition.Once }] },
        [new SqlStatement(sql)]);

    private readonly MigrationWorkflow _sut;

    public MigrationWorkflowTests()
    {
        _currentProvider
            .GetSchema(Arg.Any<SchemaSourceMode>(), Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new DatabaseSchema([]));

        _desiredProvider
            .GetProject(Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(ProjectDefinition(new DatabaseSchema([])));

        _planner.Validate(Arg.Any<DatabaseSchema>()).Returns(new PolicyDiagnostics());

        _planner
            .Plan(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>())
            .Returns(Result.Success(EmptyPlan()));

        _planner
            .PlanTeardown(Arg.Any<DatabaseSchema>())
            .Returns(Result.Success(EmptyPlan()));

        _sut = BuildSut();
    }

    [Fact]
    public async Task ValidateDesiredSchema_ReturnsNoFindings_WhenNoPolicyErrors()
    {
        // Arrange
        var desired = new DatabaseSchema([new SchemaDefinition("app")]);
        _desiredProvider.GetProject(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(ProjectDefinition(desired));

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
        var project = Result.From(new ProjectDefinition(new DatabaseSchema([]), []),
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
        var plan = new MigrationPlan(new DatabaseDiff([]), [new SqlStatement("CREATE SCHEMA app")]);
        _planner
            .Plan(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>())
            .Returns(Result.Success(plan));

        // Act
        var result = await _sut.ComputePlan(SchemaSourceMode.Offline, required: false, null, TestContext.Current.CancellationToken);

        // Assert — the result is returned for the caller to render; the workflow renders nothing itself.
        result.Value!.ShouldBe(plan);
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
            .Returns(Result.Success(
                new ProjectDefinition(desired, [new Script("seed", "select 1", new DeploymentEvent(DeploymentPhase.Post))])));
        _currentProvider
            .GetSchema(SchemaSourceMode.Online, Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new DatabaseSchema([new SchemaDefinition("app")]));

        // Act
        await _sut.ComputePlan(SchemaSourceMode.Online, required: true, null, TestContext.Current.CancellationToken);

        // Assert — verbose census is transient narration, emitted as Detail-level progress.
        _progress.Received().Report(OperationProgress.Detail("Desired schema: 1 schema, 2 tables, 1 script."));
        _progress.Received().Report(OperationProgress.Detail("Current schema (online): 1 schema, 0 tables."));
    }

    [Fact]
    public async Task ComputePlan_ReadDiagnostics_ArePrependedToThePlannerResult()
    {
        // Arrange — the pure planner never sees read provenance; this shell merges it into the outcome.
        var project = Result.From(new ProjectDefinition(new DatabaseSchema([]), []),
            [Diagnostic.Warning("deprecations", "old form")]);
        _desiredProvider.GetProject(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(project);
        _planner.Plan(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>())
            .Returns(Result.From(EmptyPlan(),
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
        var project = Result.From(new ProjectDefinition(new DatabaseSchema([]), []),
            [Diagnostic.Warning("deprecations", "old form")]);
        _desiredProvider.GetProject(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(project);
        _planner.Plan(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>())
            .Returns(Result.Failure<MigrationPlan>([Diagnostic.Error("P1", "blocked")]));

        // Act
        var result = await _sut.ComputePlan(SchemaSourceMode.Offline, required: false, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Diagnostics.Select(d => d.Source).ShouldBe(["deprecations", "P1"]);
    }

    private static Script SeedScript(RunCondition condition = RunCondition.Once) =>
        new("seed", "SELECT 1", new DeploymentEvent(DeploymentPhase.Post)) { RunCondition = condition };

    /// <summary>Builds a workflow whose store records <paramref name="executions"/> and whose DDL declares <paramref name="project"/>.</summary>
    private MigrationWorkflow SutWithState(ProjectDefinition project, params ScriptExecution[] executions)
    {
        var store = Substitute.For<ISchemaStateStore>();
        store.Read(Arg.Any<CancellationToken>())
            .Returns(_stateSerializer.Serialize(new SchemaState(new DatabaseSchema([]), executions)));
        _desiredProvider.GetProject(Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(project));
        return BuildSut(store);
    }

    [Fact]
    public async Task ComputePlan_TranslatesRecordedExecutionsIntoTheCurrentState()
    {
        // Arrange — the planner's "what I have" input is the schema plus the recorded executions; execution
        // records are shared script vocabulary, so the ledger passes straight through.
        var sut = SutWithState(new ProjectDefinition(new DatabaseSchema([]), [SeedScript()]),
            new ScriptExecution("seed", "abc", DateTimeOffset.UnixEpoch));

        // Act
        await sut.ComputePlan(SchemaSourceMode.Online, required: true, null, TestContext.Current.CancellationToken);

        // Assert
        _planner.Received(1).Plan(
            Arg.Is<CurrentState>(c => c.ExecutedScripts.Count == 1 && c.ExecutedScripts[0] == new ScriptExecution("seed", "abc", DateTimeOffset.UnixEpoch)),
            Arg.Any<ProjectDefinition>());
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
        _planner.DidNotReceive().Plan(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>());
    }

    [Fact]
    public async Task ComputePlan_WithoutStore_Fails()
    {
        // Arrange — the diff is computed against CurrentState (schema + run-once ledger), so planning without
        // a store would plan against knowingly incomplete current state.
        var sut = BuildSut(store: null);

        // Act
        var result = await sut.ComputePlan(SchemaSourceMode.Online, required: true, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("Planning requires a state store");
        _planner.DidNotReceive().Plan(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>());
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
        await sut.Refresh(AppliedPlan("seed", "SELECT 1"), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var execution = _stateSerializer.Deserialize(written!.Value).Scripts.ShouldHaveSingleItem();
        execution.Name.ShouldBe("seed");
        execution.Hash.ShouldBe(ScriptHashing.Hash("SELECT 1"));
    }

    [Fact]
    public async Task Refresh_PreservesTheExistingLedger()
    {
        // Arrange — the ledger is the one part of state a capture cannot rebuild, so it must carry over.
        var existing = new ScriptExecution("api-login", "hash", DateTimeOffset.UnixEpoch);
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
        var existing = new ScriptExecution("seed", "old-hash", DateTimeOffset.UnixEpoch);
        var store = Substitute.For<ISchemaStateStore>();
        store.Read(Arg.Any<CancellationToken>())
            .Returns(_stateSerializer.Serialize(new SchemaState(new DatabaseSchema([]), [existing])));
        ReadOnlyMemory<byte>? written = null;
        await store.Write(Arg.Do<ReadOnlyMemory<byte>>(m => written = m), Arg.Any<CancellationToken>());
        var sut = BuildSut(store);

        // Act
        await sut.Refresh(AppliedPlan("seed", "SELECT 2"), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        _stateSerializer.Deserialize(written!.Value).Scripts.ShouldHaveSingleItem().Hash.ShouldBe(ScriptHashing.Hash("SELECT 2"));
    }

    [Fact]
    public async Task Refresh_CorruptExistingState_WithoutForce_FailsWithoutWriting()
    {
        // Arrange — replacing an unreadable payload resets the run-once ledger, so it must be asked for.
        var store = Substitute.For<ISchemaStateStore>();
        store.Read(Arg.Any<CancellationToken>()).Returns((ReadOnlyMemory<byte>?)new byte[] { 0x7b });
        var sut = BuildSut(store);

        // Act
        var captured = await sut.Refresh(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        captured.ShouldNotBeNull();
        captured.IsFailure.ShouldBeTrue();
        captured.Errors.ShouldContain(d => d.Message.Contains("force"));
        await store.DidNotReceive().Write(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_CorruptExistingState_WithForce_ReplacesAndFlagsTheResetLedger()
    {
        // Arrange — a forced refresh is the recovery path for corrupt state.
        var store = Substitute.For<ISchemaStateStore>();
        store.Read(Arg.Any<CancellationToken>()).Returns((ReadOnlyMemory<byte>?)new byte[] { 0x7b });
        ReadOnlyMemory<byte>? written = null;
        await store.Write(Arg.Do<ReadOnlyMemory<byte>>(m => written = m), Arg.Any<CancellationToken>());
        var sut = BuildSut(store);

        // Act
        var captured = await sut.Refresh(force: true, cancellationToken: TestContext.Current.CancellationToken);

        // Assert — the reset ledger rides the successful capture as warnings (an error would flip the result
        // to a failure), so the caller surfaces it without any extra plumbing.
        captured.ShouldNotBeNull();
        captured.IsSuccess.ShouldBeTrue();
        captured.Diagnostics.ShouldContain(d => d.Severity == DiagnosticSeverity.Warning && d.Message.Contains("ledger was reset"));
        captured.Diagnostics.ShouldAllBe(d => d.Severity == DiagnosticSeverity.Warning);
        _stateSerializer.Deserialize(written!.Value).Scripts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Refresh_ReadableExistingState_CarriesNoDiagnostics()
    {
        // Arrange
        var store = Substitute.For<ISchemaStateStore>();
        store.Read(Arg.Any<CancellationToken>())
            .Returns(_stateSerializer.Serialize(new SchemaState(new DatabaseSchema([]))));
        var sut = BuildSut(store);

        // Act
        var captured = await sut.Refresh(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        captured.ShouldNotBeNull();
        captured.IsSuccess.ShouldBeTrue();
        captured.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task ComputePlan_PolicyViolation_ReturnsErrors_WithoutThrowingOrReporting()
    {
        // Arrange
        var errors = new[] { Diagnostic.Error("P1", "msg") };
        _planner.Plan(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>())
            .Returns(Result.Failure<MigrationPlan>(errors));

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
        _planner.Plan(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>())
            .Returns(Result.From(new MigrationPlan(diff, []), errors));

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
        var plan = EmptyPlan();
        _planner.Plan(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>())
            .Returns(Result.From(plan, diagnostics));

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
        _desiredProvider.GetProject(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(ProjectDefinition(desired));
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
            .Returns(call => { desiredScope = call.Arg<string[]?>(); return ProjectDefinition(new DatabaseSchema([])); });
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
            .Returns(call => { desiredScope = call.Arg<string[]?>(); return ProjectDefinition(new DatabaseSchema([])); });

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
    public async Task ComputeTeardown_WithoutStore_Fails()
    {
        // Arrange — the managed schema is the recorded state, so a teardown has nothing to read without a store.
        var sut = BuildSut(store: null);

        // Act
        var result = await sut.ComputeTeardown(TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("Planning requires a state store");
        _planner.DidNotReceive().PlanTeardown(Arg.Any<DatabaseSchema>());
    }

    [Fact]
    public async Task ComputeTeardown_ReturnsTeardownPlan_WithoutRendering()
    {
        // Arrange
        var plan = new MigrationPlan(new DatabaseDiff([]), [new SqlStatement("DROP SCHEMA app")]);
        _planner.PlanTeardown(Arg.Any<DatabaseSchema>()).Returns(Result.Success(plan));

        // Act
        var result = await _sut.ComputeTeardown(TestContext.Current.CancellationToken);

        // Assert
        result.Value!.ShouldBe(plan);
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
    public async Task Refresh_NoStore_Fails_WithoutContactingTheDatabase()
    {
        // Arrange
        var sut = BuildSut(store: null);

        // Act — state is where a run is recorded, so having nowhere to record is a failure, not a no-op.
        var capture = await sut.Refresh(cancellationToken: TestContext.Current.CancellationToken);

        // Assert: fails (never throws) and does not touch the live database.
        capture.IsFailure.ShouldBeTrue();
        capture.Errors.ShouldHaveSingleItem().Message.ShouldContain("without a configured state store");
        await _currentProvider.DidNotReceive().GetSchema(
            SchemaSourceMode.Online, Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }
}
