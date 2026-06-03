using NSchema.Hosting;
using NSchema.Hosting.Operations;
using NSchema.Hosting.Services;
using NSchema.Migration;
using NSchema.Migration.Plan;
using NSchema.Migration.Sources;
using NSchema.Schema;
using NSubstitute.ExceptionExtensions;

namespace NSchema.Tests.Hosting.Operations;

public sealed class ApplyOperationTests
{
    private readonly IMigrationReporter _reporter = Substitute.For<IMigrationReporter>();
    private readonly IMigrationHelper _helper = Substitute.For<IMigrationHelper>();
    private readonly IMigrationCompiler _compiler = Substitute.For<IMigrationCompiler>();
    private readonly ICompiledMigration _execution = Substitute.For<ICompiledMigration>();
    private readonly IMigrationConfirmation _confirmation = Substitute.For<IMigrationConfirmation>();

    private readonly MigrationPlan _plan = new([new CreateSchema("app")], DatabaseSchema.Create([]));

    private ApplyOperation BuildSut(IMigrationCompiler? compiler) =>
        new(_reporter, _confirmation, _helper, compiler);

    private readonly ApplyOperation _sut;

    public ApplyOperationTests()
    {
        _helper.Prepare(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(_plan);
        _helper.HasStore.Returns(true);
        _execution.Preview.Returns([]);
        _compiler.Compile(Arg.Any<MigrationPlan>(), Arg.Any<CancellationToken>()).Returns(_execution);
        _confirmation.Confirm(Arg.Any<MigrationPlan>(), Arg.Any<CancellationToken>()).Returns(true);

        _sut = BuildSut(_compiler);
    }

    [Fact]
    public async Task Execute_PreparesPlanFromOnlineSource()
    {
        await _sut.Execute();

        await _helper.Received(1).Prepare(SchemaSourceMode.Online, required: true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_CompilesAndExecutesPlan()
    {
        await _sut.Execute();

        await _compiler.Received(1).Compile(_plan, Arg.Any<CancellationToken>());
        await _execution.Received(1).Execute(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_NoCompiler_ThrowsWithoutPreparing()
    {
        var sut = BuildSut(compiler: null);

        await Should.ThrowAsync<InvalidOperationException>(() => sut.Execute());
        await _helper.DidNotReceive().Prepare(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WithStore_RefreshesStateAfterSuccess()
    {
        await _sut.Execute();

        await _helper.Received(1).Refresh(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_NoStore_DoesNotRefresh()
    {
        _helper.HasStore.Returns(false);

        await _sut.Execute();

        await _helper.DidNotReceive().Refresh(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ExecutionFails_DoesNotRefresh()
    {
        _execution.Execute(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));

        await Should.ThrowAsync<InvalidOperationException>(() => _sut.Execute());

        await _helper.DidNotReceive().Refresh(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_NotConfirmed_DoesNotExecuteOrRefresh()
    {
        _confirmation.Confirm(Arg.Any<MigrationPlan>(), Arg.Any<CancellationToken>()).Returns(false);

        await _sut.Execute();

        await _execution.DidNotReceive().Execute(Arg.Any<CancellationToken>());
        await _helper.DidNotReceive().Refresh(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_Confirmed_Executes()
    {
        _confirmation.Confirm(Arg.Any<MigrationPlan>(), Arg.Any<CancellationToken>()).Returns(true);

        await _sut.Execute();

        await _execution.Received(1).Execute(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ConfirmationPromptedAfterPreview()
    {
        _execution.Preview.Returns(["CREATE SCHEMA app;"]);

        var callOrder = new List<string>();
        _reporter.When(r => r.ReportPreview(Arg.Any<IReadOnlyList<string>>())).Do(_ => callOrder.Add("preview"));
        _confirmation.Confirm(Arg.Any<MigrationPlan>(), Arg.Any<CancellationToken>())
            .Returns(_ => { callOrder.Add("confirm"); return true; });

        await _sut.Execute();

        callOrder.ShouldBe(["preview", "confirm"]);
    }
}
