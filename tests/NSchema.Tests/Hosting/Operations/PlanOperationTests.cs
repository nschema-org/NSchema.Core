using NSchema.Hosting.Operations;
using NSchema.Hosting.Services;
using NSchema.Migration;
using NSchema.Plan.Model;
using NSchema.Schema;
using NSchema.Schema.Model;

namespace NSchema.Tests.Hosting.Operations;

public sealed class PlanOperationTests
{
    private readonly IMigrationReporter _reporter = Substitute.For<IMigrationReporter>();
    private readonly IMigrationHelper _helper = Substitute.For<IMigrationHelper>();
    private readonly IMigrationCompiler _compiler = Substitute.For<IMigrationCompiler>();
    private readonly ICompiledMigration _execution = Substitute.For<ICompiledMigration>();

    private readonly MigrationPlan _plan = new([new CreateSchema("app")], DatabaseSchema.Create([]));

    private PlanOperation BuildSut(IMigrationCompiler? compiler) => new(_reporter, _helper, compiler);

    private readonly PlanOperation _sut;

    public PlanOperationTests()
    {
        _helper.Plan(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(_plan);
        _execution.Preview.Returns([]);
        _compiler.Compile(Arg.Any<MigrationPlan>(), Arg.Any<CancellationToken>()).Returns(_execution);

        _sut = BuildSut(_compiler);
    }

    [Fact]
    public async Task Execute_PreparesPlanFromOfflineSource()
    {
        await _sut.Execute(TestContext.Current.CancellationToken);

        await _helper.Received(1).Plan(SchemaSourceMode.Offline, required: false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_CompilesButDoesNotExecute()
    {
        await _sut.Execute(TestContext.Current.CancellationToken);

        await _compiler.Received(1).Compile(_plan, Arg.Any<CancellationToken>());
        await _execution.DidNotReceive().Execute(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_PresentsPreviewToReporter()
    {
        _execution.Preview.Returns(["CREATE SCHEMA app"]);

        await _sut.Execute(TestContext.Current.CancellationToken);

        _reporter.Received(1).ReportPreview(Arg.Is<IReadOnlyList<string>>(p => p.SequenceEqual(new[] { "CREATE SCHEMA app" })));
    }

    [Fact]
    public async Task Execute_NoCompiler_ReportsPlanWithoutPreview()
    {
        var sut = BuildSut(compiler: null);

        await sut.Execute(TestContext.Current.CancellationToken);

        await _helper.Received(1).Plan(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        _reporter.DidNotReceive().ReportPreview(Arg.Any<IReadOnlyList<string>>());
    }

    [Fact]
    public async Task Execute_PrepareThrows_DoesNotCompile()
    {
        _helper.Plan(Arg.Any<SchemaSourceMode>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<MigrationPlan>(_ => throw new InvalidOperationException("boom"));

        await Should.ThrowAsync<InvalidOperationException>(() => _sut.Execute());

        await _compiler.DidNotReceive().Compile(Arg.Any<MigrationPlan>(), Arg.Any<CancellationToken>());
    }
}
