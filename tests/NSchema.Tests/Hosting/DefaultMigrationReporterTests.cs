using Microsoft.Extensions.Logging;
using NSchema.Hosting;
using NSchema.Migration;
using NSchema.Migration.Plan;
using NSchema.Policies;
using NSchema.Schema;

namespace NSchema.Tests.Hosting;

public sealed class DefaultMigrationReporterTests
{
    private readonly ILogger<DefaultMigrationReporter> _logger = Substitute.For<ILogger<DefaultMigrationReporter>>();
    private readonly IMigrationPlanRenderer _planRenderer = Substitute.For<IMigrationPlanRenderer>();

    private readonly DefaultMigrationReporter _sut;

    public DefaultMigrationReporterTests()
    {
        _sut = new DefaultMigrationReporter(_logger, _planRenderer);
    }

    [Fact]
    public void Info_LogsAtInformation()
    {
        _sut.Info("hello");

        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains("hello")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public void Error_LogsAtError()
    {
        _sut.Error("boom");

        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains("boom")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public void ReportPlan_LogsRenderedPlanAtInformation()
    {
        var plan = new MigrationPlan([], DatabaseSchema.Create([]));
        _planRenderer.Render(plan).Returns("rendered diff");

        _sut.ReportPlan(plan);

        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains("rendered diff")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public void ReportPreview_LogsEachStatementAtInformation()
    {
        _sut.ReportPreview(["CREATE SCHEMA app", "CREATE TABLE app.users (id int)"]);

        _logger.Received(2).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public void ReportDiagnostics_RoutesEachSeverityToCorrectLogLevel()
    {
        var diagnostics = new[]
        {
            new PolicyError("P1", "all good", PolicySeverity.Info),
            new PolicyError("P2", "be careful", PolicySeverity.Warning),
            new PolicyError("P3", "blocked", PolicySeverity.Error),
        };

        _sut.ReportDiagnostics(diagnostics);

        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(s => s.ToString()!.Contains("P1: all good")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(s => s.ToString()!.Contains("P2: be careful")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(s => s.ToString()!.Contains("P3: blocked")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }
}
