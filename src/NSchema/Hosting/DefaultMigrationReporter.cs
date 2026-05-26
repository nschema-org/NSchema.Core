using Microsoft.Extensions.Logging;

namespace NSchema.Hosting;

/// <summary>
/// Default <see cref="IMigrationReporter"/> that writes user-facing output directly to the console
/// and also fans out to <see cref="ILogger"/> so structured sinks (Datadog, OpenTelemetry, etc.)
/// continue to receive the migration narrative.
/// </summary>
internal sealed class DefaultMigrationReporter(ILogger<DefaultMigrationReporter> logger) : IMigrationReporter
{
    public void Info(string message) => Write(LogLevel.Information, message);

    public void Warn(string message) => Write(LogLevel.Warning, message);

    public void Error(string message) => Write(LogLevel.Error, message);

    private void Write(LogLevel level, string message)
    {
        // Forward to ILogger as a single structured Message field for downstream sinks.
        logger.Log(level, "{Message}", message);

        // Errors and warnings go to stderr so they don't get tangled with normal output when piped.
        var writer = level >= LogLevel.Warning ? Console.Error : Console.Out;
        writer.WriteLine(message);
    }
}
