using Microsoft.Extensions.Logging;
using NSchema.Hosting;

namespace NSchema.Tests.Hosting;

public sealed class DefaultMigrationReporterTests
{
    private static (DefaultMigrationReporter Reporter, ILogger<DefaultMigrationReporter> Logger) Build()
    {
        var logger = Substitute.For<ILogger<DefaultMigrationReporter>>();
        return (new DefaultMigrationReporter(logger), logger);
    }

    private static (string Out, string Err) CaptureConsole(Action action)
    {
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        Console.SetOut(stdout);
        Console.SetError(stderr);
        try
        {
            action();
            return (stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void Info_writes_to_stdout_and_logs_at_information()
    {
        var (reporter, logger) = Build();

        var (stdout, stderr) = CaptureConsole(() => reporter.Info("hello"));

        stdout.ShouldBe("hello" + Environment.NewLine);
        stderr.ShouldBeEmpty();
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains("hello")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public void Warn_writes_to_stderr_and_logs_at_warning()
    {
        var (reporter, logger) = Build();

        var (stdout, stderr) = CaptureConsole(() => reporter.Warn("careful"));

        stderr.ShouldBe("careful" + Environment.NewLine);
        stdout.ShouldBeEmpty();
        logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains("careful")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public void Error_writes_to_stderr_and_logs_at_error()
    {
        var (reporter, logger) = Build();

        var (stdout, stderr) = CaptureConsole(() => reporter.Error("boom"));

        stderr.ShouldBe("boom" + Environment.NewLine);
        stdout.ShouldBeEmpty();
        logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains("boom")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }
}