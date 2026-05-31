using Microsoft.Extensions.Logging;

namespace NSchema.Hosting;

internal sealed class NSchemaTerminalLogger : ILogger
{
    internal static readonly NSchemaTerminalLogger Instance = new();

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        var writer = logLevel >= LogLevel.Warning ? Console.Error : Console.Out;
        writer.WriteLine(message);
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
}
