using Microsoft.Extensions.Options;
using NSchema.Hosting.Operations;
using NSchema.Migration;
using NSchema.Migration.Plan;
using NSchema.Policies;
using NSchema.Schema;

namespace NSchema.Tests.Hosting.Operations;

public sealed class PlanOperationTests
{
    private readonly IMigrationPlanner _planner = Substitute.For<IMigrationPlanner>();
    private readonly IMigrationReporter _reporter = Substitute.For<IMigrationReporter>();
    private readonly IMigrationCompiler _compiler = Substitute.For<IMigrationCompiler>();
    private readonly ICompiledMigration _execution = Substitute.For<ICompiledMigration>();
    private readonly ICurrentSchemaProvider _currentProvider = Substitute.For<ICurrentSchemaProvider>();
    private readonly ISchemaAggregator _aggregator = Substitute.For<ISchemaAggregator>();
    private readonly ISchemaProvider _desiredProvider = Substitute.For<ISchemaProvider>();
    private readonly IOptions<MigrationOptions> _options = Options.Create(new MigrationOptions());

    private PlanOperation BuildSut(IMigrationCompiler? compiler) => new(
        _planner, _reporter, _currentProvider, [_desiredProvider], _aggregator, _options, compiler);

    private readonly PlanOperation _sut;

    public PlanOperationTests()
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

        _sut = BuildSut(_compiler);
    }

    [Fact]
    public async Task Execute_CompilesButDoesNotExecute()
    {
        await _sut.Execute();

        await _compiler.Received(1).Compile(Arg.Any<MigrationPlan>(), Arg.Any<CancellationToken>());
        await _execution.DidNotReceive().Execute(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_PresentsPlanAndPreviewToReporter()
    {
        var plan = new MigrationPlan([new CreateSchema("app")], DatabaseSchema.Create([]));
        _planner.Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<CancellationToken>())
            .Returns(new MigrationPlanResult(plan, []));
        _execution.Preview.Returns(["CREATE SCHEMA app"]);

        await _sut.Execute();

        _reporter.Received(1).ReportPlan(plan);
        _reporter.Received(1).ReportPreview(Arg.Is<IReadOnlyList<string>>(p => p.SequenceEqual(new[] { "CREATE SCHEMA app" })));
    }

    [Fact]
    public async Task Execute_NoCompiler_ReportsPlanWithoutPreview()
    {
        var plan = new MigrationPlan([new CreateSchema("app")], DatabaseSchema.Create([]));
        _planner.Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<CancellationToken>())
            .Returns(new MigrationPlanResult(plan, []));
        var sut = BuildSut(compiler: null);

        await sut.Execute();

        _reporter.Received(1).ReportPlan(plan);
        _reporter.DidNotReceive().ReportPreview(Arg.Any<IReadOnlyList<string>>());
    }

    [Fact]
    public async Task Execute_PolicyViolation_ReportsDiagnosticsAndThrows()
    {
        var errors = new[] { new PolicyError("P1", "msg", PolicySeverity.Error) };
        _planner.Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<CancellationToken>())
            .Returns(new MigrationPlanResult(null, errors));

        await Should.ThrowAsync<PolicyViolationException>(() => _sut.Execute());
        _reporter.Received(1).ReportDiagnostics(Arg.Any<IReadOnlyList<PolicyError>>());
        await _compiler.DidNotReceive().Compile(Arg.Any<MigrationPlan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ForwardsDiagnosticsToReporter()
    {
        var diagnostics = new[] { new PolicyError("P1", "info", PolicySeverity.Info) };
        _planner.Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<CancellationToken>())
            .Returns(new MigrationPlanResult(new MigrationPlan([], DatabaseSchema.Create([])), diagnostics));

        await _sut.Execute();

        _reporter.Received(1).ReportDiagnostics(Arg.Is<IReadOnlyList<PolicyError>>(d => d.SequenceEqual(diagnostics)));
    }

    [Fact]
    public async Task Execute_PrefersOfflineSource()
    {
        var offlineSource = Substitute.For<ISchemaProvider>();
        offlineSource.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(DatabaseSchema.Create([]));
        _currentProvider.GetSource(SchemaSourceMode.Offline, required: false).Returns(offlineSource);

        await _sut.Execute();

        await offlineSource.Received(1).GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_AggregatesAllDesiredProviders()
    {
        var s1 = DatabaseSchema.Create([SchemaDefinition.Create("a")]);
        var s2 = DatabaseSchema.Create([SchemaDefinition.Create("b")]);
        var merged = DatabaseSchema.Create([SchemaDefinition.Create("a"), SchemaDefinition.Create("b")]);
        var p1 = Substitute.For<ISchemaProvider>();
        var p2 = Substitute.For<ISchemaProvider>();
        p1.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(s1);
        p2.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(s2);
        _aggregator.Aggregate(Arg.Any<IReadOnlyList<DatabaseSchema>>()).Returns(merged);
        var sut = new PlanOperation(_planner, _reporter, _currentProvider, [p1, p2], _aggregator, _options, _compiler);

        await sut.Execute();

        _aggregator.Received(1).Aggregate(Arg.Is<IReadOnlyList<DatabaseSchema>>(l => l.Count == 2));
        await _planner.Received(1).Plan(Arg.Any<DatabaseSchema>(), merged, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_DerivesCurrentScopeFromDesiredSchema()
    {
        var desired = DatabaseSchema.Create(
            [SchemaDefinition.Create("app"), SchemaDefinition.Create("admin")],
            droppedSchemas: ["legacy"]);
        _aggregator.Aggregate(Arg.Any<IReadOnlyList<DatabaseSchema>>()).Returns(desired);
        string[]? capturedScope = null;
        var source = Substitute.For<ISchemaProvider>();
        source.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(call => { capturedScope = call.Arg<string[]?>(); return Task.FromResult(DatabaseSchema.Create([])); });
        _currentProvider.GetSource(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>()).Returns(source);

        await _sut.Execute();

        capturedScope.ShouldNotBeNull();
        capturedScope!.ShouldBe(["app", "admin", "legacy"], ignoreOrder: true);
    }

    [Fact]
    public async Task Execute_PassesExplicitScopeToDesiredAndCurrentProviders()
    {
        string[]? desiredScope = null;
        string[]? currentScope = null;
        _desiredProvider.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(call => { desiredScope = call.Arg<string[]?>(); return Task.FromResult(DatabaseSchema.Create([])); });
        var source = Substitute.For<ISchemaProvider>();
        source.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(call => { currentScope = call.Arg<string[]?>(); return Task.FromResult(DatabaseSchema.Create([])); });
        _currentProvider.GetSource(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>()).Returns(source);
        _options.Value.SchemaNames = ["app", "legacy"];

        await _sut.Execute();

        desiredScope.ShouldBe(["app", "legacy"]);
        currentScope.ShouldBe(["app", "legacy"]);
    }

    [Fact]
    public async Task Execute_PassesNullScopeToDesiredProviders_WhenNoExplicitScope()
    {
        string[]? desiredScope = [];
        _desiredProvider.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(call => { desiredScope = call.Arg<string[]?>(); return Task.FromResult(DatabaseSchema.Create([])); });

        await _sut.Execute();

        desiredScope.ShouldBeNull();
    }
}
