using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSchema.Hosting;
using NSchema.Migration;
using NSubstitute.ExceptionExtensions;

namespace NSchema.Tests.Hosting;

public sealed class NSchemaHostTests
{
    private readonly MigrationRunOptions _options = new();
    private readonly IMigrationOperation _planOp = Substitute.For<IMigrationOperation>();
    private readonly IMigrationOperation _applyOp = Substitute.For<IMigrationOperation>();
    private readonly IMigrationOperation _refreshOp = Substitute.For<IMigrationOperation>();
    private readonly IHostApplicationLifetime _lifetime = Substitute.For<IHostApplicationLifetime>();
    private readonly IMigrationReporter _reporter = Substitute.For<IMigrationReporter>();

    private NSchemaHost BuildHost()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IMigrationOperation>(MigrationOperation.Plan, (_, _) => _planOp);
        services.AddKeyedSingleton<IMigrationOperation>(MigrationOperation.Apply, (_, _) => _applyOp);
        services.AddKeyedSingleton<IMigrationOperation>(MigrationOperation.Refresh, (_, _) => _refreshOp);
        var sp = services.BuildServiceProvider();
        return new NSchemaHost(Options.Create(_options), _lifetime, sp, _reporter);
    }

    [Fact]
    public async Task Execute_PlanOperation_RunsPlanAndStops()
    {
        _options.Operation = MigrationOperation.Plan;
        var sut = BuildHost();

        await sut.StartAsync(CancellationToken.None);
        await sut.ExecuteTask!;

        await _planOp.Received(1).Execute(Arg.Any<CancellationToken>());
        await _applyOp.DidNotReceive().Execute(Arg.Any<CancellationToken>());
        _lifetime.Received(1).StopApplication();
    }

    [Fact]
    public async Task Execute_ApplyOperation_RunsApplyAndStops()
    {
        _options.Operation = MigrationOperation.Apply;
        var sut = BuildHost();

        await sut.StartAsync(CancellationToken.None);
        await sut.ExecuteTask!;

        await _applyOp.Received(1).Execute(Arg.Any<CancellationToken>());
        await _planOp.DidNotReceive().Execute(Arg.Any<CancellationToken>());
        _lifetime.Received(1).StopApplication();
    }

    [Fact]
    public async Task Execute_RefreshOperation_RunsRefreshAndStops()
    {
        _options.Operation = MigrationOperation.Refresh;
        var sut = BuildHost();

        await sut.StartAsync(CancellationToken.None);
        await sut.ExecuteTask!;

        await _refreshOp.Received(1).Execute(Arg.Any<CancellationToken>());
        await _applyOp.DidNotReceive().Execute(Arg.Any<CancellationToken>());
        await _planOp.DidNotReceive().Execute(Arg.Any<CancellationToken>());
        _lifetime.Received(1).StopApplication();
    }

    [Fact]
    public async Task Execute_StopsApplication_WhenOperationThrows()
    {
        _options.Operation = MigrationOperation.Apply;
        _applyOp.Execute(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));
        var sut = BuildHost();
        await sut.StartAsync(CancellationToken.None);

        await Should.ThrowAsync<InvalidOperationException>(() => sut.ExecuteTask!);
        _lifetime.Received(1).StopApplication();
    }

    [Fact]
    public async Task Execute_UnexpectedException_ReportsErrorBeforeRethrowing()
    {
        _options.Operation = MigrationOperation.Apply;
        _applyOp.Execute(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));
        var sut = BuildHost();
        await sut.StartAsync(CancellationToken.None);

        await Should.ThrowAsync<InvalidOperationException>(() => sut.ExecuteTask!);
        _reporter.Received(1).Error(Arg.Is<string>(s => s.Contains("boom")));
    }
}
