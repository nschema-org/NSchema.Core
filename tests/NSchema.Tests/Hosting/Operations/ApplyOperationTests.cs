using Microsoft.Extensions.Options;
using NSchema.Hosting.Operations;
using NSchema.Migration;
using NSchema.Migration.Plan;
using NSchema.Policies;
using NSchema.Schema;
using NSchema.State;
using NSubstitute.ExceptionExtensions;

namespace NSchema.Tests.Hosting.Operations;

public sealed class ApplyOperationTests
{
    private readonly IMigrationPlanner _planner = Substitute.For<IMigrationPlanner>();
    private readonly IMigrationReporter _reporter = Substitute.For<IMigrationReporter>();
    private readonly IMigrationCompiler _compiler = Substitute.For<IMigrationCompiler>();
    private readonly ICompiledMigration _execution = Substitute.For<ICompiledMigration>();
    private readonly ISchemaStateStore _store = Substitute.For<ISchemaStateStore>();
    private readonly ICurrentSchemaProvider _currentProvider = Substitute.For<ICurrentSchemaProvider>();
    private readonly IDesiredSchemaProvider _desiredProvider = Substitute.For<IDesiredSchemaProvider>();
    private readonly IOptions<MigrationOptions> _options = Options.Create(new MigrationOptions());

    private ApplyOperation BuildSut(IMigrationCompiler? compiler, ISchemaStateStore? store = null) =>
        new(_options, _planner, _reporter, _currentProvider, _desiredProvider, store, compiler);

    private readonly ApplyOperation _sut;

    public ApplyOperationTests()
    {
        _currentProvider
            .GetSchema(Arg.Any<SchemaSourceMode>(), Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(DatabaseSchema.Create([]));

        _desiredProvider.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(DatabaseSchema.Create([]));
        _planner
            .Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<CancellationToken>())
            .Returns(new MigrationPlanResult(new MigrationPlan([], DatabaseSchema.Create([])), []));
        _execution.Preview.Returns([]);
        _compiler.Compile(Arg.Any<MigrationPlan>(), Arg.Any<CancellationToken>()).Returns(_execution);

        _sut = BuildSut(_compiler);
    }

    [Fact]
    public async Task Execute_CompilesAndExecutesPlan()
    {
        var plan = new MigrationPlan([new CreateSchema("app")], DatabaseSchema.Create([]));
        _planner.Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<CancellationToken>())
            .Returns(new MigrationPlanResult(plan, []));

        await _sut.Execute();

        await _compiler.Received(1).Compile(plan, Arg.Any<CancellationToken>());
        await _execution.Received(1).Execute(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WithStore_CapturesStateAfterSuccess()
    {
        var sut = BuildSut(_compiler, _store);

        await sut.Execute();

        await _store.Received(1).Write(Arg.Any<DatabaseSchema>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WithStore_DoesNotCaptureWhenExecutionFails()
    {
        _execution.Execute(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));
        var sut = BuildSut(_compiler, _store);

        await Should.ThrowAsync<InvalidOperationException>(() => sut.Execute());

        await _store.DidNotReceive().Write(Arg.Any<DatabaseSchema>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_NoStore_DoesNotAttemptCapture()
    {
        await _sut.Execute();

        await _store.DidNotReceive().Write(Arg.Any<DatabaseSchema>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_NoCompiler_ThrowsWithoutPlanning()
    {
        var sut = BuildSut(compiler: null);

        await Should.ThrowAsync<InvalidOperationException>(() => sut.Execute());
        await _planner.DidNotReceive().Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_PolicyViolation_ReportsDiagnosticsAndThrows()
    {
        var errors = new[]
        {
            new PolicyError("P1", "msg1", PolicySeverity.Error),
            new PolicyError("P2", "msg2", PolicySeverity.Error),
        };
        _planner.Plan(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>(), Arg.Any<CancellationToken>())
            .Returns(new MigrationPlanResult(null, errors));

        await Should.ThrowAsync<PolicyViolationException>(() => _sut.Execute());
        _reporter.Received(1).ReportDiagnostics(Arg.Is<IReadOnlyList<PolicyError>>(d =>
            d.Any(e => e.Message == "msg1") && d.Any(e => e.Message == "msg2")));
        await _compiler.DidNotReceive().Compile(Arg.Any<MigrationPlan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Execute_ExecutionThrows_ReportsErrorAndRethrows()
    {
        _execution.Execute(Arg.Any<CancellationToken>()).Throws(new InvalidOperationException("boom"));

        var act = async () => await _sut.Execute();

        act.ShouldThrow<InvalidOperationException>();
        _reporter.Received().Error(Arg.Is<string>(s => s.Contains("boom")));
    }

    [Fact]
    public async Task Execute_AlwaysUsesOnlineSource()
    {
        await _sut.Execute();

        await _currentProvider.Received().GetSchema(
            SchemaSourceMode.Online, Arg.Any<string[]?>(), required: true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_EmptyPlan_StillExecutes()
    {
        await _sut.Execute();
        await _execution.Received(1).Execute(Arg.Any<CancellationToken>());
    }
}
