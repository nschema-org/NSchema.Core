using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSchema.Hosting;
using NSchema.Migration;
using NSchema.Operations;
using NSubstitute.ExceptionExtensions;

namespace NSchema.Tests.Hosting;

public sealed class NSchemaHostTests
{
    private readonly OperationOptions _options = new();
    private readonly INSchemaOperation _planOp = Substitute.For<INSchemaOperation>();
    private readonly INSchemaOperation _applyOp = Substitute.For<INSchemaOperation>();
    private readonly INSchemaOperation _refreshOp = Substitute.For<INSchemaOperation>();
    private readonly IHostApplicationLifetime _lifetime = Substitute.For<IHostApplicationLifetime>();
    private readonly IMigrationReporter _reporter = Substitute.For<IMigrationReporter>();
    private readonly OperationResult _outcome = new();

    private readonly NSchemaHost _sut;

    public NSchemaHostTests()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<INSchemaOperation>(MigrationOperation.Plan, (_, _) => _planOp);
        services.AddKeyedSingleton<INSchemaOperation>(MigrationOperation.Apply, (_, _) => _applyOp);
        services.AddKeyedSingleton<INSchemaOperation>(MigrationOperation.Refresh, (_, _) => _refreshOp);
        var sp = services.BuildServiceProvider();

        _sut = new NSchemaHost(Options.Create(_options), _lifetime, sp, Helpers.TestReporters.ResolverFor(_reporter), _outcome);
    }

    [Fact]
    public async Task Execute_PlanOperation_RunsPlanAndStops()
    {
        // Arrange
        _options.Operation = MigrationOperation.Plan;

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
        _options.Operation = MigrationOperation.Apply;

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
        _options.Operation = MigrationOperation.Refresh;

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
        _options.Operation = MigrationOperation.Apply;
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
        _options.Operation = MigrationOperation.Apply;
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
        _options.Operation = MigrationOperation.Apply;
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
