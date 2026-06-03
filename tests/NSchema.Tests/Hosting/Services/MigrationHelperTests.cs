using Microsoft.Extensions.Options;
using NSchema.Diff.Model;
using NSchema.Hosting.Services;
using NSchema.Migration;
using NSchema.Plan.Model;
using NSchema.Policies;
using NSchema.Schema;
using NSchema.Schema.Model;
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

        _desiredProvider
            .GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(DatabaseSchema.Create([]));

        _planner
            .Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<CancellationToken>())
            .Returns(new MigrationPlanResult(new MigrationPlan([], DatabaseSchema.Create([])), new MigrationDiff([], [], []), []));

        _sut = BuildSut();
    }

    [Fact]
    public async Task Prepare_ReturnsComputedPlan_AndReportsItsDiff()
    {
        // Arrange
        var plan = new MigrationPlan([new CreateSchema("app")], DatabaseSchema.Create([]));
        var diff = new MigrationDiff([], [], []);
        _planner
            .Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<CancellationToken>())
            .Returns(new MigrationPlanResult(plan, diff, []));

        // Act
        var result = await _sut.Plan(SchemaSourceMode.Offline, required: false, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe(plan);
        _reporter.Received(1).ReportDiff(diff);
    }

    [Fact]
    public async Task Prepare_ForwardsSourceModeAndRequiredToCurrentProvider()
    {
        // Arrange

        // Act
        await _sut.Plan(SchemaSourceMode.Online, required: true, TestContext.Current.CancellationToken);

        // Assert
        await _currentProvider.Received(1).GetSchema(
            SchemaSourceMode.Online, Arg.Any<string[]?>(), required: true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Prepare_PrefersOfflineSourceWithFallback()
    {
        // Arrange

        // Act
        await _sut.Plan(SchemaSourceMode.Offline, required: false, TestContext.Current.CancellationToken);

        // Assert
        await _currentProvider.Received(1).GetSchema(
            SchemaSourceMode.Offline, Arg.Any<string[]?>(), required: false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Prepare_PolicyViolation_ReportsDiagnosticsAndThrows_WithoutShowingPlan()
    {
        // Arrange
        var errors = new[] { new PolicyError("P1", "msg") };
        _planner.Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<CancellationToken>())
            .Returns(new MigrationPlanResult(null, null, errors));

        // Act
        var act = () => _sut.Plan(SchemaSourceMode.Offline, required: false, TestContext.Current.CancellationToken);

        // Assert
        await act.ShouldThrowAsync<PolicyViolationException>();
        _reporter.Received(1).ReportDiagnostics(Arg.Any<IReadOnlyList<PolicyError>>());
        _reporter.DidNotReceive().ReportDiff(Arg.Any<MigrationDiff>());
    }

    [Fact]
    public async Task Prepare_NonErrorDiagnostics_ReportedAfterDiff()
    {
        // Arrange
        var diagnostics = new[] { new PolicyError("P1", "info", PolicySeverity.Info) };
        var plan = new MigrationPlan([], DatabaseSchema.Create([]));
        _planner.Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<CancellationToken>())
            .Returns(new MigrationPlanResult(plan, new MigrationDiff([], [], []), diagnostics));

        var callOrder = new List<string>();
        _reporter.When(r => r.ReportDiff(Arg.Any<MigrationDiff>())).Do(_ => callOrder.Add("diff"));
        _reporter.When(r => r.ReportDiagnostics(Arg.Any<IReadOnlyList<PolicyError>>())).Do(_ => callOrder.Add("diagnostics"));

        // Act
        await _sut.Plan(SchemaSourceMode.Offline, required: false, TestContext.Current.CancellationToken);

        // Assert
        _reporter.Received(1).ReportDiagnostics(Arg.Is<IReadOnlyList<PolicyError>>(d => d.SequenceEqual(diagnostics)));
        callOrder.ShouldBe(["diff", "diagnostics"]);
    }

    [Fact]
    public async Task Prepare_DerivesScopeFromDesiredSchema_WhenNoExplicitScope()
    {
        // Arrange
        var desired = DatabaseSchema.Create(
            [SchemaDefinition.Create("app"), SchemaDefinition.Create("admin")],
            droppedSchemas: ["legacy"]);
        _desiredProvider.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(desired);
        string[]? capturedScope = null;
        _currentProvider
            .GetSchema(Arg.Any<SchemaSourceMode>(), Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(call => { capturedScope = call.ArgAt<string[]?>(1); return Task.FromResult(DatabaseSchema.Create([])); });

        // Act
        await _sut.Plan(SchemaSourceMode.Offline, required: false, TestContext.Current.CancellationToken);

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
            .Returns(call => { desiredScope = call.Arg<string[]?>(); return Task.FromResult(DatabaseSchema.Create([])); });
        _currentProvider
            .GetSchema(Arg.Any<SchemaSourceMode>(), Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(call => { currentScope = call.ArgAt<string[]?>(1); return Task.FromResult(DatabaseSchema.Create([])); });
        _options.Value.SchemaNames = ["app", "legacy"];

        // Act
        await _sut.Plan(SchemaSourceMode.Offline, required: false, TestContext.Current.CancellationToken);

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
            .Returns(call => { desiredScope = call.Arg<string[]?>(); return Task.FromResult(DatabaseSchema.Create([])); });

        // Act
        await _sut.Plan(SchemaSourceMode.Offline, required: false, TestContext.Current.CancellationToken);

        // Assert
        desiredScope.ShouldBeNull();
    }

    [Fact]
    public void HasStore_WithoutStore_ReturnsFalse()
    {
        // Arrange
        var sut = BuildSut(store: null);

        // Act
        var result = sut.HasStore;

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void HasStore_WithStore_ReturnsTrue()
    {
        // Arrange
        var sut = BuildSut(Substitute.For<ISchemaStateStore>());

        // Act
        var result = sut.HasStore;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task Refresh_WritesLiveSchemaToStore_Unscoped()
    {
        // Arrange
        var schema = DatabaseSchema.Create([SchemaDefinition.Create("app")]);
        var store = Substitute.For<ISchemaStateStore>();
        _currentProvider
            .GetSchema(SchemaSourceMode.Online, Arg.Any<string[]?>(), required: true, Arg.Any<CancellationToken>())
            .Returns(schema);
        var sut = BuildSut(store);

        // Assert
        await sut.Refresh(TestContext.Current.CancellationToken);

        await _currentProvider.Received(1).GetSchema(
            SchemaSourceMode.Online, Arg.Is<string[]?>(names => names == null), required: true, Arg.Any<CancellationToken>());
        await store.Received(1).Write(schema, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_NoStore_Throws()
    {
        // Arrange
        var sut = BuildSut(store: null);

        // Act
        var act = () => sut.Refresh();

        // Assert
        await act.ShouldThrowAsync<InvalidOperationException>();
    }
}
