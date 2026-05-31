using Microsoft.Extensions.Options;
using NSchema.Hosting;
using NSchema.Migration;
using NSchema.Migration.Plan;
using NSchema.Policies;
using NSchema.Schema;
using NSubstitute.ExceptionExtensions;

namespace NSchema.Tests.Hosting;

public sealed class DefaultMigrationPipelineTests
{
    private readonly IMigrationPlanner _planner = Substitute.For<IMigrationPlanner>();
    private readonly IMigrationReporter _reporter = Substitute.For<IMigrationReporter>();
    private readonly IMigrationCompiler _compiler = Substitute.For<IMigrationCompiler>();
    private readonly ICompiledMigration _execution = Substitute.For<ICompiledMigration>();
    private readonly IStateCapturer _stateCapturer = Substitute.For<IStateCapturer>();
    private readonly ICurrentSchemaProvider _currentProvider = Substitute.For<ICurrentSchemaProvider>();
    private readonly ISchemaAggregator _aggregator = Substitute.For<ISchemaAggregator>();
    private readonly ISchemaProvider _desiredProvider = Substitute.For<ISchemaProvider>();
    private readonly IOptions<MigrationOptions> _options = Options.Create(new MigrationOptions());

    private readonly DefaultMigrationPipeline _sut;

    public DefaultMigrationPipelineTests()
    {
        var mockSource = Substitute.For<ISchemaProvider>();
        mockSource.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(DatabaseSchema.Create([]));
        _currentProvider.GetSource(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>()).Returns(mockSource);

        _desiredProvider.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(DatabaseSchema.Create([]));
        _aggregator.Aggregate(Arg.Any<IReadOnlyList<DatabaseSchema>>())
            .Returns(DatabaseSchema.Create([]));

        _planner
            .Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<CancellationToken>())
            .Returns(new MigrationPlanResult(new MigrationPlan([], DatabaseSchema.Create([])), []));

        _execution.Preview.Returns([]);
        _compiler.Compile(Arg.Any<MigrationPlan>(), Arg.Any<CancellationToken>()).Returns(_execution);

        _sut = new DefaultMigrationPipeline(_options, _planner, _reporter, _stateCapturer, _currentProvider, [_desiredProvider], _aggregator, _compiler);
    }

    [Fact]
    public async Task Plan_CompilesButDoesNotExecute()
    {
        await _sut.Plan();

        await _compiler.Received(1).Compile(Arg.Any<MigrationPlan>(), Arg.Any<CancellationToken>());
        await _execution.DidNotReceive().Execute(Arg.Any<CancellationToken>());
        await _stateCapturer.DidNotReceive().Capture(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Apply_CompilesExecutesAndCapturesState()
    {
        // Arrange
        var plan = new MigrationPlan([new CreateSchema("app")], DatabaseSchema.Create([]));
        _planner.Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<CancellationToken>())
            .Returns(new MigrationPlanResult(plan, []));

        // Act
        await _sut.Apply();

        // Assert
        await _compiler.Received(1).Compile(plan, Arg.Any<CancellationToken>());
        await _execution.Received(1).Execute(Arg.Any<CancellationToken>());
        await _stateCapturer.Received(1).Capture(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Apply_DoesNotCaptureState_WhenExecutionFails()
    {
        _execution.Execute(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));

        await Should.ThrowAsync<InvalidOperationException>(() => _sut.Apply());
        await _stateCapturer.DidNotReceive().Capture(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_CapturesStateWithoutPlanningOrCompiling()
    {
        _stateCapturer.Capture(Arg.Any<CancellationToken>()).Returns(true);

        await _sut.Refresh();

        await _stateCapturer.Received(1).Capture(Arg.Any<CancellationToken>());
        await _planner.DidNotReceive().Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<CancellationToken>());
        await _compiler.DidNotReceive().Compile(Arg.Any<MigrationPlan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_NoStateStore_Throws()
    {
        _stateCapturer.Capture(Arg.Any<CancellationToken>()).Returns(false);
        await Should.ThrowAsync<InvalidOperationException>(() => _sut.Refresh());
    }

    [Fact]
    public async Task Apply_EmptyPlan_StillExecutes()
    {
        await _sut.Apply();
        await _execution.Received(1).Execute(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Plan_PresentsPlanAndPreviewToReporter()
    {
        // Arrange
        var plan = new MigrationPlan([new CreateSchema("app")], DatabaseSchema.Create([]));
        _planner.Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<CancellationToken>())
            .Returns(new MigrationPlanResult(plan, []));
        _execution.Preview.Returns(["CREATE SCHEMA app", "CREATE TABLE app.users (id int)"]);

        // Act
        await _sut.Plan();

        // Assert
        _reporter.Received(1).ReportPlan(plan);
        _reporter.Received(1).ReportPreview(Arg.Is<IReadOnlyList<string>>(p =>
            p.SequenceEqual(new[] { "CREATE SCHEMA app", "CREATE TABLE app.users (id int)" })));
    }

    [Fact]
    public async Task Apply_PolicyViolation_ReportsDiagnosticsAndThrows()
    {
        // Arrange
        var errors = new[]
        {
            new PolicyError("P1", "msg1", PolicySeverity.Error),
            new PolicyError("P2", "msg2", PolicySeverity.Error),
        };
        _planner
            .Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<CancellationToken>())
            .Returns(new MigrationPlanResult(null, errors));

        // Act / Assert
        await Should.ThrowAsync<PolicyViolationException>(() => _sut.Apply());
        _reporter.Received(1).ReportDiagnostics(Arg.Is<IReadOnlyList<PolicyError>>(d =>
            d.Any(e => e.Message == "msg1") && d.Any(e => e.Message == "msg2")));
        await _compiler.DidNotReceive().Compile(Arg.Any<MigrationPlan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Apply_ExecutionThrows_ReportsErrorAndRethrows()
    {
        _execution.Execute(Arg.Any<CancellationToken>()).Throws(new InvalidOperationException("boom"));

        var act = async () => await _sut.Apply();

        act.ShouldThrow<InvalidOperationException>();
        _reporter.Received().Error(Arg.Is<string>(s => s.Contains("boom")));
    }

    [Fact]
    public async Task Plan_NoCompiler_ReportsPlanWithoutPreview()
    {
        // Arrange
        var plan = new MigrationPlan([new CreateSchema("app")], DatabaseSchema.Create([]));
        _planner.Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<CancellationToken>())
            .Returns(new MigrationPlanResult(plan, []));
        var sut = new DefaultMigrationPipeline(_options, _planner, _reporter, _stateCapturer, _currentProvider, [_desiredProvider], _aggregator, compiler: null);

        await sut.Plan();

        _reporter.Received(1).ReportPlan(plan);
        _reporter.DidNotReceive().ReportPreview(Arg.Any<IReadOnlyList<string>>());
    }

    [Fact]
    public async Task Apply_NoCompiler_Throws()
    {
        var sut = new DefaultMigrationPipeline(_options, _planner, _reporter, _stateCapturer, _currentProvider, [_desiredProvider], _aggregator, compiler: null);

        await Should.ThrowAsync<InvalidOperationException>(() => sut.Apply());
        await _planner.DidNotReceive().Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<CancellationToken>());
        await _stateCapturer.DidNotReceive().Capture(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Plan_ForwardsDiagnosticsToReporter()
    {
        var diagnostics = new[]
        {
            new PolicyError("P1", "info message", PolicySeverity.Info),
            new PolicyError("P2", "warning message", PolicySeverity.Warning),
        };
        _planner.Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<CancellationToken>())
            .Returns(new MigrationPlanResult(new MigrationPlan([], DatabaseSchema.Create([])), diagnostics));

        await _sut.Plan();

        _reporter.Received(1).ReportDiagnostics(Arg.Is<IReadOnlyList<PolicyError>>(d => d.SequenceEqual(diagnostics)));
    }

    // --- Source selection ---

    [Fact]
    public async Task Plan_PrefersOfflineSource_WhenAvailable()
    {
        var offlineSource = Substitute.For<ISchemaProvider>();
        offlineSource.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(DatabaseSchema.Create([]));
        _currentProvider.GetSource(SchemaSourceMode.Offline, required: false).Returns(offlineSource);

        await _sut.Plan();

        // The current schema passed to the planner came from the offline source.
        await offlineSource.Received(1).GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Plan_FallsBackToOnlineSource_WhenNoOffline()
    {
        var onlineSource = Substitute.For<ISchemaProvider>();
        onlineSource.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(DatabaseSchema.Create([]));
        _currentProvider.GetSource(SchemaSourceMode.Offline, required: false).Returns(onlineSource);

        await _sut.Plan();

        await onlineSource.Received(1).GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Apply_AlwaysUsesOnlineSource()
    {
        var onlineSource = Substitute.For<ISchemaProvider>();
        onlineSource.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(DatabaseSchema.Create([]));
        _currentProvider.GetSource(SchemaSourceMode.Online, required: true).Returns(onlineSource);

        await _sut.Apply();

        await onlineSource.Received(1).GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>());
    }

    // --- Schema resolution and scope ---

    [Fact]
    public async Task Plan_AggregatesAllDesiredProvidersBeforePlanning()
    {
        // Arrange
        var s1 = DatabaseSchema.Create([SchemaDefinition.Create("a")]);
        var s2 = DatabaseSchema.Create([SchemaDefinition.Create("b")]);
        var merged = DatabaseSchema.Create([SchemaDefinition.Create("a"), SchemaDefinition.Create("b")]);
        var p1 = Substitute.For<ISchemaProvider>();
        var p2 = Substitute.For<ISchemaProvider>();
        p1.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(s1);
        p2.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(s2);
        _aggregator.Aggregate(Arg.Any<IReadOnlyList<DatabaseSchema>>()).Returns(merged);
        var sut = new DefaultMigrationPipeline(_options, _planner, _reporter, _stateCapturer, _currentProvider, [p1, p2], _aggregator, _compiler);

        await sut.Plan();

        _aggregator.Received(1).Aggregate(Arg.Is<IReadOnlyList<DatabaseSchema>>(l => l.Count == 2));
        await _planner.Received(1).Plan(Arg.Any<DatabaseSchema>(), merged, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Plan_DerivesCurrentScopeFromDesiredSchema_WhenNoExplicitScope()
    {
        // Arrange: desired schema declares "app" and drops "legacy"; current read should be scoped to both.
        var desired = DatabaseSchema.Create(
            [SchemaDefinition.Create("app"), SchemaDefinition.Create("admin")],
            droppedSchemas: ["legacy"]);
        _aggregator.Aggregate(Arg.Any<IReadOnlyList<DatabaseSchema>>()).Returns(desired);
        string[]? capturedScope = null;
        var source = Substitute.For<ISchemaProvider>();
        source.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(call => { capturedScope = call.Arg<string[]?>(); return Task.FromResult(DatabaseSchema.Create([])); });
        _currentProvider.GetSource(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>()).Returns(source);

        await _sut.Plan();

        capturedScope.ShouldNotBeNull();
        capturedScope!.ShouldBe(["app", "admin", "legacy"], ignoreOrder: true);
    }

    [Fact]
    public async Task Plan_PassesExplicitScopeToDesiredAndCurrentProviders()
    {
        // Arrange
        string[]? desiredScope = null;
        string[]? currentScope = null;
        _desiredProvider.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(call => { desiredScope = call.Arg<string[]?>(); return Task.FromResult(DatabaseSchema.Create([])); });
        var source = Substitute.For<ISchemaProvider>();
        source.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(call => { currentScope = call.Arg<string[]?>(); return Task.FromResult(DatabaseSchema.Create([])); });
        _currentProvider.GetSource(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>()).Returns(source);
        _options.Value.SchemaNames = ["app", "legacy"];

        await _sut.Plan();

        desiredScope.ShouldBe(["app", "legacy"]);
        currentScope.ShouldBe(["app", "legacy"]);
    }

    [Fact]
    public async Task Plan_PassesNullScopeToDesiredProviders_WhenNoExplicitScope()
    {
        string[]? desiredScope = [];
        _desiredProvider.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(call => { desiredScope = call.Arg<string[]?>(); return Task.FromResult(DatabaseSchema.Create([])); });

        await _sut.Plan();

        desiredScope.ShouldBeNull();
    }
}
