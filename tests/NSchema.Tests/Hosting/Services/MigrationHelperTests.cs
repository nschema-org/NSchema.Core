using Microsoft.Extensions.Options;
using NSchema.Hosting.Services;
using NSchema.Migration;
using NSchema.Migration.Plan;
using NSchema.Policies;
using NSchema.Schema;
using NSchema.State;

namespace NSchema.Tests.Hosting.Services;

public sealed class MigrationHelperTests
{
    private readonly IMigrationPlanner _planner = Substitute.For<IMigrationPlanner>();
    private readonly IMigrationReporter _reporter = Substitute.For<IMigrationReporter>();
    private readonly ICurrentSchemaProvider _currentProvider = Substitute.For<ICurrentSchemaProvider>();
    private readonly IDesiredSchemaProvider _desiredProvider = Substitute.For<IDesiredSchemaProvider>();
    private readonly IOptions<MigrationOptions> _options = Options.Create(new MigrationOptions());

    private MigrationHelper BuildSut(ISchemaStateStore? store = null) =>
        new(_options, _planner, _reporter, _currentProvider, _desiredProvider, store);

    private readonly MigrationHelper _sut;

    public MigrationHelperTests()
    {
        _currentProvider
            .GetSchema(Arg.Any<SchemaSourceMode>(), Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(DatabaseSchema.Create([]));
        _desiredProvider.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(DatabaseSchema.Create([]));
        _planner
            .Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<CancellationToken>())
            .Returns(new MigrationPlanResult(new MigrationPlan([], DatabaseSchema.Create([])), []));

        _sut = BuildSut();
    }

    [Fact]
    public async Task Prepare_ReturnsComputedPlan()
    {
        var plan = new MigrationPlan([new CreateSchema("app")], DatabaseSchema.Create([]));
        _planner.Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<CancellationToken>())
            .Returns(new MigrationPlanResult(plan, []));

        var result = await _sut.Prepare(SchemaSourceMode.Offline, required: false);

        result.ShouldBe(plan);
        _reporter.Received(1).ReportPlan(plan);
    }

    [Fact]
    public async Task Prepare_ForwardsSourceModeAndRequiredToCurrentProvider()
    {
        await _sut.Prepare(SchemaSourceMode.Online, required: true);

        await _currentProvider.Received(1).GetSchema(
            SchemaSourceMode.Online, Arg.Any<string[]?>(), required: true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Prepare_PrefersOfflineSourceWithFallback()
    {
        await _sut.Prepare(SchemaSourceMode.Offline, required: false);

        await _currentProvider.Received(1).GetSchema(
            SchemaSourceMode.Offline, Arg.Any<string[]?>(), required: false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Prepare_PolicyViolation_ReportsDiagnosticsAndThrows_WithoutShowingPlan()
    {
        var errors = new[] { new PolicyError("P1", "msg", PolicySeverity.Error) };
        _planner.Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<CancellationToken>())
            .Returns(new MigrationPlanResult(null, errors));

        await Should.ThrowAsync<PolicyViolationException>(() => _sut.Prepare(SchemaSourceMode.Offline, required: false));

        _reporter.Received(1).ReportDiagnostics(Arg.Any<IReadOnlyList<PolicyError>>());
        _reporter.DidNotReceive().ReportPlan(Arg.Any<MigrationPlan>());
    }

    [Fact]
    public async Task Prepare_NonErrorDiagnostics_ReportedAfterPlan()
    {
        var diagnostics = new[] { new PolicyError("P1", "info", PolicySeverity.Info) };
        var plan = new MigrationPlan([], DatabaseSchema.Create([]));
        _planner.Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<CancellationToken>())
            .Returns(new MigrationPlanResult(plan, diagnostics));

        var callOrder = new List<string>();
        _reporter.When(r => r.ReportPlan(Arg.Any<MigrationPlan>())).Do(_ => callOrder.Add("plan"));
        _reporter.When(r => r.ReportDiagnostics(Arg.Any<IReadOnlyList<PolicyError>>())).Do(_ => callOrder.Add("diagnostics"));

        await _sut.Prepare(SchemaSourceMode.Offline, required: false);

        _reporter.Received(1).ReportDiagnostics(Arg.Is<IReadOnlyList<PolicyError>>(d => d.SequenceEqual(diagnostics)));
        callOrder.ShouldBe(["plan", "diagnostics"]);
    }

    [Fact]
    public async Task Prepare_DerivesScopeFromDesiredSchema_WhenNoExplicitScope()
    {
        var desired = DatabaseSchema.Create(
            [SchemaDefinition.Create("app"), SchemaDefinition.Create("admin")],
            droppedSchemas: ["legacy"]);
        _desiredProvider.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(desired);
        string[]? capturedScope = null;
        _currentProvider
            .GetSchema(Arg.Any<SchemaSourceMode>(), Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(call => { capturedScope = call.ArgAt<string[]?>(1); return Task.FromResult(DatabaseSchema.Create([])); });

        await _sut.Prepare(SchemaSourceMode.Offline, required: false);

        capturedScope.ShouldNotBeNull();
        capturedScope!.ShouldBe(["app", "admin", "legacy"], ignoreOrder: true);
    }

    [Fact]
    public async Task Prepare_PassesExplicitScopeToDesiredAndCurrentProviders()
    {
        string[]? desiredScope = null;
        string[]? currentScope = null;
        _desiredProvider.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(call => { desiredScope = call.Arg<string[]?>(); return Task.FromResult(DatabaseSchema.Create([])); });
        _currentProvider
            .GetSchema(Arg.Any<SchemaSourceMode>(), Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(call => { currentScope = call.ArgAt<string[]?>(1); return Task.FromResult(DatabaseSchema.Create([])); });
        _options.Value.SchemaNames = ["app", "legacy"];

        await _sut.Prepare(SchemaSourceMode.Offline, required: false);

        desiredScope.ShouldBe(["app", "legacy"]);
        currentScope.ShouldBe(["app", "legacy"]);
    }

    [Fact]
    public async Task Prepare_PassesNullScopeToDesiredProvider_WhenNoExplicitScope()
    {
        string[]? desiredScope = [];
        _desiredProvider.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(call => { desiredScope = call.Arg<string[]?>(); return Task.FromResult(DatabaseSchema.Create([])); });

        await _sut.Prepare(SchemaSourceMode.Offline, required: false);

        desiredScope.ShouldBeNull();
    }

    [Fact]
    public void HasStore_ReflectsStorePresence()
    {
        BuildSut(store: null).HasStore.ShouldBeFalse();
        BuildSut(Substitute.For<ISchemaStateStore>()).HasStore.ShouldBeTrue();
    }

    [Fact]
    public async Task Refresh_WritesLiveSchemaToStore_Unscoped()
    {
        var schema = DatabaseSchema.Create([SchemaDefinition.Create("app")]);
        var store = Substitute.For<ISchemaStateStore>();
        _currentProvider
            .GetSchema(SchemaSourceMode.Online, Arg.Any<string[]?>(), required: true, Arg.Any<CancellationToken>())
            .Returns(schema);
        var sut = BuildSut(store);

        await sut.Refresh();

        await _currentProvider.Received(1).GetSchema(
            SchemaSourceMode.Online, Arg.Is<string[]?>(names => names == null), required: true, Arg.Any<CancellationToken>());
        await store.Received(1).Write(schema, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_NoStore_Throws()
    {
        await Should.ThrowAsync<InvalidOperationException>(() => BuildSut(store: null).Refresh());
    }
}
