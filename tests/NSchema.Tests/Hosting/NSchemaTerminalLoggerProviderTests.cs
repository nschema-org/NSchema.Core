using Microsoft.Extensions.Logging;
using NSchema.Hosting;

namespace NSchema.Tests.Hosting;

public sealed class NSchemaTerminalLoggerProviderTests
{
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

    private static void Log(LogLevel level, string message) =>
        NSchemaTerminalLogger.Instance.Log(level, default, message, null, (s, _) => s);

    [Fact]
    public void Information_WritesToStdout()
    {
        var (stdout, stderr) = CaptureConsole(() => Log(LogLevel.Information, "hello"));

        stdout.ShouldBe("hello" + Environment.NewLine);
        stderr.ShouldBeEmpty();
    }

    [Fact]
    public void Warning_WritesToStderr()
    {
        var (stdout, stderr) = CaptureConsole(() => Log(LogLevel.Warning, "careful"));

        stderr.ShouldBe("careful" + Environment.NewLine);
        stdout.ShouldBeEmpty();
    }

    [Fact]
    public void Error_WritesToStderr()
    {
        var (stdout, stderr) = CaptureConsole(() => Log(LogLevel.Error, "boom"));

        stderr.ShouldBe("boom" + Environment.NewLine);
        stdout.ShouldBeEmpty();
    }

    [Fact]
    public void EmptyMessage_WritesNothing()
    {
        var (stdout, stderr) = CaptureConsole(() => Log(LogLevel.Information, ""));

        stdout.ShouldBeEmpty();
        stderr.ShouldBeEmpty();
    }

    [Fact]
    public void CreateLogger_ReturnsSameInstance()
    {
        using var provider = new NSchemaTerminalLoggerProvider();
        var a = provider.CreateLogger("Foo");
        var b = provider.CreateLogger("Bar");
        a.ShouldBeSameAs(b);
    }
}
