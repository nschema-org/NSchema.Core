using NSchema.Model.Services;
using NSchema.Project.Domain.Models;
using NSchema.Deployment;
using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Models;
using NSchema.Operations;
using NSchema.Operations.Progress;
using NSchema.Operations.Workflow;
using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Models;
using NSchema.Project;
using NSchema.Model;
using NSchema.Model.Schemas;
using NSchema.Model.Scripts;
using NSchema.Model.Tables;
using NSchema.State;
using NSchema.State.Backends;
using NSchema.State.Domain.Models;

namespace NSchema.Tests.Operations;

public sealed class MigrationWorkflowTests
{
    private readonly IMigrationPlanner _planner = Substitute.For<IMigrationPlanner>();
    private readonly IProgress<OperationProgress> _progress = Substitute.For<IProgress<OperationProgress>>();
    private readonly IDatabaseProvider _currentProvider = Substitute.For<IDatabaseProvider>();
    private readonly IProjectProvider _desiredProvider = Substitute.For<IProjectProvider>();
    private readonly IDatabaseStateSerializer _stateSerializer = new DatabaseStateSerializer();

    private MigrationWorkflow BuildSut(IDatabaseStateStore? store) =>
        new(_planner, _progress, _currentProvider, _desiredProvider, new DatabaseStateManager(_stateSerializer, store));

    /// <summary>Planning requires a store, so the default fixture carries an empty in-memory one.</summary>
    private MigrationWorkflow BuildSut() => BuildSut(new EphemeralStateStore());

    private static Result<ProjectDefinition> ProjectDefinition(Database schema) => Result.Success(new ProjectDefinition(schema));

    private static MigrationPlan EmptyPlan() => new(new DatabaseDiff([]), []);

    /// <summary>An applied plan carrying one run-once script, so the capture has an execution to record.</summary>
    private static MigrationPlan AppliedPlan(string name, string sql) => new(
        new DatabaseDiff([]) { DeploymentScripts = [new DeploymentScript(new SqlIdentifier(name), new SqlText(sql), null, DeploymentPhase.Post) { RunCondition = RunCondition.Once }] },
        [new SqlStatement(new SqlText(sql))]);

    private readonly MigrationWorkflow _sut;

    public MigrationWorkflowTests()
    {
        _currentProvider
            .GetDatabase(Arg.Any<DatabaseScope>(), Arg.Any<CancellationToken>())
            .Returns(new Database([]));

        _desiredProvider
            .GetProject(Arg.Any<DatabaseScope>(), Arg.Any<CancellationToken>())
            .Returns(ProjectDefinition(new Database([])));

        _planner.Validate(Arg.Any<ProjectDefinition>()).Returns(Result.Success());

        _planner
            .Plan(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>())
            .Returns(Result.Success(EmptyPlan()));

        _sut = BuildSut();
    }

    /// <summary>Builds a workflow whose store records <paramref name="recorded"/> as the current schema.</summary>
    private MigrationWorkflow SutWithRecordedSchema(Database recorded)
    {
        var store = Substitute.For<IDatabaseStateStore>();
        store.Read(Arg.Any<CancellationToken>()).Returns(_stateSerializer.Serialize(new DatabaseState(recorded)));
        return BuildSut(store);
    }

    [Fact]
    public async Task ValidateDesiredSchema_ReturnsNoFindings_WhenNoPolicyErrors()
    {
        // Arrange
        var desired = new Database([new Schema(new SqlIdentifier("app"))]);
        _desiredProvider.GetProject(Arg.Any<DatabaseScope>(), Arg.Any<CancellationToken>()).Returns(ProjectDefinition(desired));

        // Act
        var findings = await _sut.Validate(TestContext.Current.CancellationToken);

        // Assert — the findings are data, not a pass/fail verdict; a clean schema yields none.
        findings.IsSuccess.ShouldBeTrue();
        findings.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task ValidateDesiredSchema_PolicyViolation_ReturnsErrorFindings_WithoutReporting()
    {
        // Arrange
        _planner.Validate(Arg.Any<ProjectDefinition>()).Returns(Result.From(Diagnostic.Error("P1", "msg")));

        // Act
        var findings = await _sut.Validate(TestContext.Current.CancellationToken);

        // Assert — the findings are carried back as data, not thrown; the workflow no longer renders them (the caller does).
        findings.IsFailure.ShouldBeTrue();
        findings.Errors.ShouldHaveSingleItem().Message.ShouldBe("msg");
    }

    [Fact]
    public async Task ValidateDesiredSchema_NonErrorDiagnostics_AreCarriedInTheFindings()
    {
        // Arrange
        _planner.Validate(Arg.Any<ProjectDefinition>())
            .Returns(Result.From(new Diagnostic("P1", "info", DiagnosticSeverity.Info)));

        // Act
        var findings = await _sut.Validate(TestContext.Current.CancellationToken);

        // Assert — advisories ride along in the findings rather than being reported here.
        findings.IsSuccess.ShouldBeTrue();
        findings.Diagnostics.ShouldHaveSingleItem().Message.ShouldBe("info");
    }

    [Fact]
    public async Task ValidateDesiredSchema_ProjectDiagnostics_AreCarriedInTheFindings()
    {
        // Arrange — findings raised while reading the DDL (e.g. deprecated syntax) arrive on the read result.
        var project = Result.From(TestProjects.Project(new Database([])), [Diagnostic.Warning("deprecations", "old form")]);
        _desiredProvider.GetProject(Arg.Any<DatabaseScope>(), Arg.Any<CancellationToken>()).Returns(project);

        // Act
        var findings = await _sut.Validate(TestContext.Current.CancellationToken);

        // Assert
        findings.IsSuccess.ShouldBeTrue();
        findings.Diagnostics.ShouldHaveSingleItem().Source.ShouldBe("deprecations");
    }

    [Fact]
    public async Task ValidateDesiredSchema_DoesNotContactCurrentProvider()
    {
        // Act
        await _sut.Validate(TestContext.Current.CancellationToken);

        // Assert
        await _currentProvider.DidNotReceive().GetDatabase(Arg.Any<DatabaseScope>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ComputePlan_ReturnsComputedPlan_WithoutRendering()
    {
        // Arrange
        var plan = new MigrationPlan(new DatabaseDiff([]), [new SqlStatement(new SqlText("CREATE SCHEMA app"))]);
        _planner
            .Plan(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>())
            .Returns(Result.Success(plan));

        // Act
        var result = await _sut.ComputePlan(PlanTarget.Project, DatabaseScope.All, TestContext.Current.CancellationToken);

        // Assert — the result is returned for the caller to render; the workflow renders nothing itself.
        result.Value!.ShouldBe(plan);
    }

    [Theory]
    [InlineData(PlanTarget.Project)]
    [InlineData(PlanTarget.Empty)]
    public async Task ComputePlan_NeverTouchesTheLiveDatabase(PlanTarget target)
    {
        // Planning is always a pure state read — the current side is the recorded database, never the live one.

        // Act
        await _sut.ComputePlan(target, DatabaseScope.All, TestContext.Current.CancellationToken);

        // Assert
        await _currentProvider.DidNotReceive().GetDatabase(Arg.Any<DatabaseScope>(), Arg.Any<CancellationToken>());
        _planner.Received(1).Plan(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>());
    }

    [Fact]
    public async Task ComputePlan_PlansTheRecordedSchemaAsTheCurrentSide()
    {
        // Arrange
        var sut = SutWithRecordedSchema(new Database([new Schema(new SqlIdentifier("app"), Tables: [new Table(new SqlIdentifier("users"))])]));

        // Act
        await sut.ComputePlan(PlanTarget.Project, DatabaseScope.All, TestContext.Current.CancellationToken);

        // Assert — the database round-trips through the store, so match it by content, not reference.
        _planner.Received(1).Plan(
            Arg.Is<CurrentState>(c => c!.Database.Schemas.Select(s => s.Name.Value).SequenceEqual(new[] { "app" })),
            Arg.Any<ProjectDefinition>());
    }

    [Fact]
    public async Task Prepare_ReportsVerboseObjectCensusForBothSchemas()
    {
        // Arrange
        var desired = new Database([new Schema(new SqlIdentifier("app"), Tables:
            [new Table(new SqlIdentifier("users")), new Table(new SqlIdentifier("orders"))])]);
        _desiredProvider.GetProject(Arg.Any<DatabaseScope>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(
                TestProjects.Project(desired, [new DeploymentScript(new SqlIdentifier("seed"), new SqlText("select 1"), null, DeploymentPhase.Post)])));
        var sut = SutWithRecordedSchema(new Database([new Schema(new SqlIdentifier("app"))]));

        // Act
        await sut.ComputePlan(PlanTarget.Project, DatabaseScope.All, TestContext.Current.CancellationToken);

        // Assert — verbose census is transient narration, emitted as Detail-level progress.
        _progress.Received().Report(OperationProgress.Detail("Desired schema: 1 schema, 2 tables, 1 script."));
        _progress.Received().Report(OperationProgress.Detail("Current schema: 1 schema, 0 tables."));
    }

    [Fact]
    public async Task ComputePlan_Teardown_ReadsNoProject()
    {
        // A teardown converges on nothing, so there is no project to read.

        // Act
        await _sut.ComputePlan(PlanTarget.Empty, DatabaseScope.All, TestContext.Current.CancellationToken);

        // Assert
        await _desiredProvider.DidNotReceive().GetProject(Arg.Any<DatabaseScope>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ComputePlan_Teardown_PlansTowardsAnEmptySchema()
    {
        // Arrange — teardown is not a third kind of plan: it is the recorded schema diffed against nothing.
        var sut = SutWithRecordedSchema(new Database([new Schema(new SqlIdentifier("app"))]));

        // Act
        await sut.ComputePlan(PlanTarget.Empty, DatabaseScope.All, TestContext.Current.CancellationToken);

        // Assert
        _planner.Received(1).Plan(
            Arg.Is<CurrentState>(c => c!.Database.Schemas.Select(s => s.Name.Value).SequenceEqual(new[] { "app" })),
            Arg.Is<ProjectDefinition>(p => p!.Database.Schemas.Count == 0));
    }

    [Fact]
    public async Task ComputePlan_Teardown_ScopesTheRecordedSchema()
    {
        // Arrange — scoping is no longer special-cased; a partial teardown narrows like any other plan.
        var sut = SutWithRecordedSchema(new Database(
            [new Schema(new SqlIdentifier("app")), new Schema(new SqlIdentifier("billing"))]));

        // Act
        await sut.ComputePlan(PlanTarget.Empty, DatabaseScope.Of(new SqlIdentifier("app")), TestContext.Current.CancellationToken);

        // Assert — only the scoped schema is torn down.
        _planner.Received(1).Plan(
            Arg.Is<CurrentState>(c => c!.Database.Schemas.Select(s => s.Name.Value).SequenceEqual(new[] { "app" })),
            Arg.Any<ProjectDefinition>());
    }

    [Fact]
    public async Task ComputePlan_Teardown_Unscoped_CoversEverythingRecorded()
    {
        // Arrange — an empty target declares no managed schemas, so scope derivation leaves the run unrestricted.
        var sut = SutWithRecordedSchema(new Database(
            [new Schema(new SqlIdentifier("app")), new Schema(new SqlIdentifier("billing"))]));

        // Act
        await sut.ComputePlan(PlanTarget.Empty, DatabaseScope.All, TestContext.Current.CancellationToken);

        // Assert
        _planner.Received(1).Plan(
            Arg.Is<CurrentState>(c => c!.Database.Schemas.Select(s => s.Name.Value).SequenceEqual(new[] { "app", "billing" })),
            Arg.Any<ProjectDefinition>());
    }

    [Fact]
    public async Task ComputePlan_Teardown_RunsPolicies()
    {
        // Arrange — a teardown genuinely is destructive, so the finding is correct rather than noise. It must
        // stay possible, but it does not have to be easy.
        _planner.Plan(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>())
            .Returns(Result.From(EmptyPlan(), [Diagnostic.Error("destructive", "drops everything")]));

        // Act
        var result = await _sut.ComputePlan(PlanTarget.Empty, DatabaseScope.All, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldBe("drops everything");
    }

    [Fact]
    public async Task ComputePlan_Teardown_WithoutStore_Fails()
    {
        // Arrange — the managed schema is the recorded state, so a teardown has nothing to read without a store.
        var sut = BuildSut(store: null);

        // Act
        var result = await sut.ComputePlan(PlanTarget.Empty, DatabaseScope.All, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().ShouldBe(WorkflowDiagnostics.StoreRequiredForPlanning);
        _planner.DidNotReceive().Plan(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>());
    }

    [Fact]
    public async Task ComputePlan_ReadDiagnostics_ArePrependedToThePlannerResult()
    {
        // Arrange — the pure planner never sees read provenance; this shell merges it into the outcome.
        var project = Result.From(TestProjects.Project(new Database([])), [Diagnostic.Warning("deprecations", "old form")]);
        _desiredProvider.GetProject(Arg.Any<DatabaseScope>(), Arg.Any<CancellationToken>()).Returns(project);
        _planner.Plan(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>())
            .Returns(Result.From(EmptyPlan(),
                [Diagnostic.Warning("data-hazards", "hazard")]));

        // Act
        var result = await _sut.ComputePlan(PlanTarget.Project, DatabaseScope.All, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.Select(d => d.Source).ShouldBe(["deprecations", "data-hazards"]);
    }

    [Fact]
    public async Task ComputePlan_ReadDiagnostics_AreCarriedOnAPlannerFailureToo()
    {
        // Arrange
        var project = Result.From(TestProjects.Project(new Database([])), [Diagnostic.Warning("deprecations", "old form")]);
        _desiredProvider.GetProject(Arg.Any<DatabaseScope>(), Arg.Any<CancellationToken>()).Returns(project);
        _planner.Plan(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>())
            .Returns(Result.Failure<MigrationPlan>([Diagnostic.Error("P1", "blocked")]));

        // Act
        var result = await _sut.ComputePlan(PlanTarget.Project, DatabaseScope.All, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Diagnostics.Select(d => d.Source).ShouldBe(["deprecations", "P1"]);
    }

    private static Script SeedScript(RunCondition condition = RunCondition.Once) =>
        new DeploymentScript(new SqlIdentifier("seed"), new SqlText("SELECT 1"), null, DeploymentPhase.Post) { RunCondition = condition };

    /// <summary>Builds a workflow whose store records <paramref name="executions"/> and whose DDL declares <paramref name="project"/>.</summary>
    private MigrationWorkflow SutWithState(ProjectDefinition project, params ScriptExecution[] executions)
    {
        var store = Substitute.For<IDatabaseStateStore>();
        store.Read(Arg.Any<CancellationToken>())
            .Returns(_stateSerializer.Serialize(new DatabaseState(new Database([]), executions)));
        _desiredProvider.GetProject(Arg.Any<DatabaseScope>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(project));
        return BuildSut(store);
    }

    [Fact]
    public async Task ComputePlan_TranslatesRecordedExecutionsIntoTheCurrentState()
    {
        // Arrange — the planner's "what I have" input is the schema plus the recorded executions; execution
        // records are shared script vocabulary, so the ledger passes straight through.
        var sut = SutWithState(TestProjects.Project(new Database([]), [SeedScript()]),
            new ScriptExecution(new ScriptReference(null, new SqlIdentifier("seed")), "abc", DateTimeOffset.UnixEpoch));

        // Act
        await sut.ComputePlan(PlanTarget.Project, DatabaseScope.All, TestContext.Current.CancellationToken);

        // Assert
        _planner.Received(1).Plan(
            Arg.Is<CurrentState>(c => c!.ExecutedScripts.Count == 1 && c.ExecutedScripts[0] == new ScriptExecution(new ScriptReference(null, new SqlIdentifier("seed")), "abc", DateTimeOffset.UnixEpoch)),
            Arg.Any<ProjectDefinition>());
    }

    [Fact]
    public async Task ComputePlan_UnreadableState_IsAFailure_WithoutPlanning()
    {
        // Arrange — an unreadable ledger must fail the plan rather than read as empty: an empty ledger
        // would re-plan every recorded run-once script.
        var store = Substitute.For<IDatabaseStateStore>();
        store.Read(Arg.Any<CancellationToken>()).Returns((ReadOnlyMemory<byte>?)"not a state payload"u8.ToArray());
        var sut = BuildSut(store);

        // Act
        var result = await sut.ComputePlan(PlanTarget.Project, DatabaseScope.All, TestContext.Current.CancellationToken);

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
        var result = await sut.ComputePlan(PlanTarget.Project, DatabaseScope.All, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().ShouldBe(WorkflowDiagnostics.StoreRequiredForPlanning);
        _planner.DidNotReceive().Plan(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>());
    }

    [Fact]
    public async Task Refresh_RecordsExecutedScripts()
    {
        // Arrange
        var store = Substitute.For<IDatabaseStateStore>();
        ReadOnlyMemory<byte>? written = null;
        await store.Write(Arg.Do<ReadOnlyMemory<byte>>(m => written = m), Arg.Any<CancellationToken>());
        var sut = BuildSut(store);

        // Act
        await sut.Refresh(AppliedPlan("seed", "SELECT 1"), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var execution = _stateSerializer.Deserialize(written!.Value).Scripts.ShouldHaveSingleItem();
        execution.Script.Name.ShouldBe("seed");
        execution.Hash.ShouldBe(ScriptHashing.Hash(new SqlText("SELECT 1")));
    }

    [Fact]
    public async Task Refresh_PreservesTheExistingLedger()
    {
        // Arrange — the ledger is the one part of state a capture cannot rebuild, so it must carry over.
        var existing = new ScriptExecution(new ScriptReference(null, new SqlIdentifier("api-login")), "hash", DateTimeOffset.UnixEpoch);
        var store = Substitute.For<IDatabaseStateStore>();
        store.Read(Arg.Any<CancellationToken>())
            .Returns(_stateSerializer.Serialize(new DatabaseState(new Database([]), [existing])));
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
        var existing = new ScriptExecution(new ScriptReference(null, new SqlIdentifier("seed")), "old-hash", DateTimeOffset.UnixEpoch);
        var store = Substitute.For<IDatabaseStateStore>();
        store.Read(Arg.Any<CancellationToken>())
            .Returns(_stateSerializer.Serialize(new DatabaseState(new Database([]), [existing])));
        ReadOnlyMemory<byte>? written = null;
        await store.Write(Arg.Do<ReadOnlyMemory<byte>>(m => written = m), Arg.Any<CancellationToken>());
        var sut = BuildSut(store);

        // Act
        await sut.Refresh(AppliedPlan("seed", "SELECT 2"), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        _stateSerializer.Deserialize(written!.Value).Scripts.ShouldHaveSingleItem().Hash.ShouldBe(ScriptHashing.Hash(new SqlText("SELECT 2")));
    }

    [Fact]
    public async Task Refresh_CorruptExistingState_WithoutForce_FailsWithoutWriting()
    {
        // Arrange — replacing an unreadable payload resets the run-once ledger, so it must be asked for.
        var store = Substitute.For<IDatabaseStateStore>();
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
        var store = Substitute.For<IDatabaseStateStore>();
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
        var store = Substitute.For<IDatabaseStateStore>();
        store.Read(Arg.Any<CancellationToken>())
            .Returns(_stateSerializer.Serialize(new DatabaseState(new Database([]))));
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
        var result = await _sut.ComputePlan(PlanTarget.Project, DatabaseScope.All, TestContext.Current.CancellationToken);

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
        var result = await _sut.ComputePlan(PlanTarget.Project, DatabaseScope.All, TestContext.Current.CancellationToken);

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
        var result = await _sut.ComputePlan(PlanTarget.Project, DatabaseScope.All, TestContext.Current.CancellationToken);

        // Assert — advisories ride in the result, rendered by the caller, not reported here.
        result.Diagnostics.ShouldBe(diagnostics);
    }

    [Fact]
    public async Task Prepare_DerivesScopeFromDesiredSchema_WhenNoExplicitScope()
    {
        // Arrange
        // The derived scope covers the declared schemas plus every schema the directives address (a dropped
        // schema and a rename's current name are under management too).
        var desired = new Database(
            [new Schema(new SqlIdentifier("app")), new Schema(new SqlIdentifier("admin"))]);
        var directives = new ProjectDirectives(new SchemaDirectives(
            Drops: [new SqlIdentifier("legacy")]));
        _desiredProvider.GetProject(Arg.Any<DatabaseScope>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ProjectDefinition(desired, directives)));
        // The recorded state carries an unmanaged schema too, so the derived scope is observable in what the
        // current side keeps.
        var sut = SutWithRecordedSchema(new Database([
            new Schema(new SqlIdentifier("app")),
            new Schema(new SqlIdentifier("admin")),
            new Schema(new SqlIdentifier("legacy")),
            new Schema(new SqlIdentifier("unmanaged")),
        ]));

        // Act
        await sut.ComputePlan(PlanTarget.Project, DatabaseScope.All, TestContext.Current.CancellationToken);

        // Assert — the diff never plans to drop unmanaged schemas.
        _planner.Received(1).Plan(
            Arg.Is<CurrentState>(c => c!.Database.Schemas.Select(s => s.Name.Value).SequenceEqual(new[] { "app", "admin", "legacy" })),
            Arg.Any<ProjectDefinition>());
    }

    [Fact]
    public async Task Prepare_PassesExplicitScopeToTheProjectProvider()
    {
        // Arrange
        DatabaseScope? desiredScope = null;
        _desiredProvider.GetProject(Arg.Any<DatabaseScope>(), Arg.Any<CancellationToken>())
            .Returns(call => { desiredScope = call.Arg<DatabaseScope>(); return ProjectDefinition(new Database([])); });

        // Act
        await _sut.ComputePlan(PlanTarget.Project, DatabaseScope.Of(new SqlIdentifier("app"), new SqlIdentifier("legacy")), TestContext.Current.CancellationToken);

        // Assert — the project read is load-bearing: template instances bind at aggregation.
        desiredScope!.SchemaNames.ShouldBe(["app", "legacy"]);
    }

    [Fact]
    public async Task Prepare_AppliesExplicitScopeToTheCurrentSide()
    {
        // Arrange
        var sut = SutWithRecordedSchema(new Database(
            [new Schema(new SqlIdentifier("app")), new Schema(new SqlIdentifier("other"))]));

        // Act
        await sut.ComputePlan(PlanTarget.Project, DatabaseScope.Of(new SqlIdentifier("app")), TestContext.Current.CancellationToken);

        // Assert — scope applies symmetrically, so both sides of the diff cover the same schemas.
        _planner.Received(1).Plan(
            Arg.Is<CurrentState>(c => c!.Database.Schemas.Select(s => s.Name.Value).SequenceEqual(new[] { "app" })),
            Arg.Any<ProjectDefinition>());
    }

    [Fact]
    public async Task Prepare_PassesTheAllScopeToTheDesiredProvider_WhenNoExplicitScope()
    {
        // Arrange
        DatabaseScope? desiredScope = null;
        _desiredProvider.GetProject(Arg.Any<DatabaseScope>(), Arg.Any<CancellationToken>())
            .Returns(call => { desiredScope = call.Arg<DatabaseScope>(); return ProjectDefinition(new Database([])); });

        // Act
        await _sut.ComputePlan(PlanTarget.Project, DatabaseScope.All, TestContext.Current.CancellationToken);

        // Assert — the project read is unrestricted; only the current read gets the derived scope.
        desiredScope.ShouldNotBeNull();
        desiredScope!.IsAll.ShouldBeTrue();
    }

    [Fact]
    public async Task Refresh_WritesLiveSchemaToStore_Unscoped()
    {
        // Arrange
        var schema = new Database([new Schema(new SqlIdentifier("app"))]);
        var store = Substitute.For<IDatabaseStateStore>();
        _currentProvider
            .GetDatabase(Arg.Any<DatabaseScope>(), Arg.Any<CancellationToken>())
            .Returns(schema);
        var sut = BuildSut(store);

        var expected = _stateSerializer.Serialize(new DatabaseState(schema)).ToArray();

        // Act
        var capture = await sut.Refresh(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        capture.ShouldNotBeNull();
        await _currentProvider.Received(1).GetDatabase(Arg.Is<DatabaseScope>(s => s!.IsAll), Arg.Any<CancellationToken>());
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
        await _currentProvider.DidNotReceive().GetDatabase(Arg.Any<DatabaseScope>(), Arg.Any<CancellationToken>());
    }
}
