using Microsoft.Extensions.Logging;

namespace NSchema.Hosting;

/// <summary>
/// A minimal logging provider that writes plain-text messages to the terminal: information and below to
/// stdout, warnings and errors to stderr. Registered by default in <see cref="NSchemaApplicationBuilder"/>
/// with a category filter so that only NSchema's own reporter output reaches the console.
/// </summary>
internal sealed class NSchemaTerminalLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => NSchemaTerminalLogger.Instance;
    public void Dispose() { }
}
