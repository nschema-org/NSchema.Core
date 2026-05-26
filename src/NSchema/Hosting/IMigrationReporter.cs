namespace NSchema.Hosting;

/// <summary>
/// Reports user-facing progress and outcomes for a migration run. Distinct from <c>ILogger</c>:
/// reporter calls are intended for the human operator at the terminal, whereas <c>ILogger</c>
/// is intended for diagnostic sinks (Datadog, OpenTelemetry, file logs, etc.). The default
/// implementation fans out to both so structured sinks still capture the migration narrative.
/// Messages are passed verbatim — use string interpolation at the call site for substitution.
/// </summary>
public interface IMigrationReporter
{
    /// <summary>Reports an informational message to the user.</summary>
    void Info(string message);

    /// <summary>Reports a warning to the user.</summary>
    void Warn(string message);

    /// <summary>Reports an error to the user.</summary>
    void Error(string message);
}
