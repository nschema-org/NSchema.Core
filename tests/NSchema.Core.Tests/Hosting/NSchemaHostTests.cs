using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSchema.Hosting;
using NSchema.Operations;
using NSubstitute.ExceptionExtensions;
using HostOptions = NSchema.Hosting.HostOptions;

namespace NSchema.Tests.Hosting;

public sealed class NSchemaHostTests
{
    private readonly HostOptions _options = new();
    private readonly IOperation _planOp = Substitute.For<IOperation>();
    private readonly IOperation _applyOp = Substitute.For<IOperation>();
    private readonly IOperation _refreshOp = Substitute.For<IOperation>();
    private readonly IHostApplicationLifetime _lifetime = Substitute.For<IHostApplicationLifetime>();
    private readonly IOperationReporter _reporter = Substitute.For<IOperationReporter>();
    private readonly OperationResult _outcome = new();

    private readonly NSchemaHost _sut;

    public NSchemaHostTests()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IOperation>(Operation.Plan, (_, _) => _planOp);
        services.AddKeyedSingleton<IOperation>(Operation.Apply, (_, _) => _applyOp);
        services.AddKeyedSingleton<IOperation>(Operation.Refresh, (_, _) => _refreshOp);
        var sp = services.BuildServiceProvider();

        _sut = new NSchemaHost(Options.Create(_options), _lifetime, sp, Helpers.TestReporters.ResolverFor(_reporter), _outcome);
    }

    [Fact]
    public async Task Execute_PlanOperation_RunsPlanAndStops()
    {
        // Arrange
        _options.Operation = Operation.Plan;

        // Act
        await _sut.StartAsync(CancellationToken.None);
        await _sut.ExecuteTask!;

        // Assert
        await _planOp.Received(1).Execute(Arg.Any<CancellationToken>());
        await _applyOp.DidNotReceive().Execute(Arg.Any<CancellationToken>());
        _lifetime.Received(1).StopApplication();
    }

    [Fact]
    public async Task Execute_ApplyOperation_RunsApplyAndStops()
    {
        // Arrange
        _options.Operation = Operation.Apply;

        // Act
        await _sut.StartAsync(CancellationToken.None);
        await _sut.ExecuteTask!;

        // Assert
        await _applyOp.Received(1).Execute(Arg.Any<CancellationToken>());
        await _planOp.DidNotReceive().Execute(Arg.Any<CancellationToken>());
        _lifetime.Received(1).StopApplication();
    }

    [Fact]
    public async Task Execute_RefreshOperation_RunsRefreshAndStops()
    {
        // Arrange
        _options.Operation = Operation.Refresh;

        // Act
        await _sut.StartAsync(CancellationToken.None);
        await _sut.ExecuteTask!;

        // Assert
        await _refreshOp.Received(1).Execute(Arg.Any<CancellationToken>());
        await _applyOp.DidNotReceive().Execute(Arg.Any<CancellationToken>());
        await _planOp.DidNotReceive().Execute(Arg.Any<CancellationToken>());
        _lifetime.Received(1).StopApplication();
    }

    [Fact]
    public async Task Execute_StopsApplication_WhenOperationThrows()
    {
        // Arrange
        _options.Operation = Operation.Apply;
        var boom = new InvalidOperationException("boom");
        _applyOp.Execute(Arg.Any<CancellationToken>()).ThrowsAsync(boom);

        // Act
        await _sut.StartAsync(CancellationToken.None);
        await _sut.ExecuteTask!;

        // Assert
        _lifetime.Received(1).StopApplication();
        _outcome.Exception.ShouldBe(boom);
    }

    [Fact]
    public async Task Execute_UnexpectedException_ReportsErrorAndCapturesFailure()
    {
        // Arrange
        _options.Operation = Operation.Apply;
        var boom = new InvalidOperationException("boom");
        _applyOp.Execute(Arg.Any<CancellationToken>()).ThrowsAsync(boom);

        // Act
        await _sut.StartAsync(CancellationToken.None);
        await _sut.ExecuteTask!;

        // Assert
        _reporter.Received(1).ReportException(boom);
        _outcome.Exception.ShouldBe(boom);
    }

    [Fact]
    public async Task Execute_ThrowBehavior_CapturesFailureWithoutReporting()
    {
        // Arrange
        _options.Operation = Operation.Apply;
        _options.ExceptionBehavior = ExceptionBehavior.Throw;
        var boom = new InvalidOperationException("boom");
        _applyOp.Execute(Arg.Any<CancellationToken>()).ThrowsAsync(boom);

        // Act
        await _sut.StartAsync(CancellationToken.None);
        await _sut.ExecuteTask!;

        // Assert
        _reporter.DidNotReceive().ReportException(Arg.Any<Exception>());
        _outcome.Exception.ShouldBe(boom);
    }
}
