using NSchema.Migration;

namespace NSchema.Hosting;

/// <summary>
/// Resolves a registered <see cref="IMigrationReporter"/> by its output format, and selects the one for
/// the configured run via <see cref="Current"/>.
/// </summary>
public interface IMigrationReporterResolver
{
    /// <summary>
    /// The distinct output formats that can be resolved, e.g. <c>human</c>, <c>json</c>.
    /// </summary>
    IReadOnlyCollection<string> AvailableFormats { get; }

    /// <summary>
    /// The reporter for the run's configured output format
    /// (<see cref="MigrationRunOptions.OutputFormat"/>).
    /// </summary>
    /// <exception cref="InvalidOperationException">No reporter is registered for the configured format.</exception>
    IMigrationReporter Current { get; }

    /// <summary>
    /// Resolves the reporter registered for <paramref name="format"/> (case-insensitive).
    /// </summary>
    /// <param name="format">The output format, e.g. <c>human</c>.</param>
    /// <exception cref="InvalidOperationException">No reporter is registered for the format.</exception>
    IMigrationReporter ForFormat(string format);

    /// <summary>
    /// Attempts to resolve the reporter registered for <paramref name="format"/> (case-insensitive).
    /// </summary>
    /// <param name="format">The output format, e.g. <c>human</c>.</param>
    /// <param name="reporter">The resolved reporter, or <see langword="null"/> if none is registered.</param>
    /// <returns><see langword="true"/> if a reporter was found; otherwise <see langword="false"/>.</returns>
    bool TryForFormat(string format, out IMigrationReporter? reporter);
}
