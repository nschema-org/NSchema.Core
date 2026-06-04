using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;
using NSchema.Migration;

namespace NSchema.Hosting;

/// <summary>
/// Resolves <see cref="IMigrationReporter"/>s from the registered candidates.
/// </summary>
internal sealed class DefaultMigrationReporterResolver(IOptions<MigrationRunOptions> options, IEnumerable<IMigrationReporter> registrations) : IMigrationReporterResolver
{
    private readonly IReadOnlyList<IMigrationReporter> _reporters = [.. registrations];

    /// <inheritdoc/>
    public IReadOnlyCollection<string> AvailableFormats
        => [.. _reporters.Select(r => r.Format).Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <inheritdoc/>
    public IMigrationReporter Current => ForFormat(options.Value.OutputFormat);

    /// <inheritdoc/>
    public IMigrationReporter ForFormat(string format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);

        if (TryForFormat(format, out var reporter))
        {
            return reporter;
        }

        var available = AvailableFormats.Count == 0
            ? "none"
            : string.Join(", ", AvailableFormats.Order(StringComparer.OrdinalIgnoreCase));
        throw new InvalidOperationException(
            $"No reporter registered for output format '{format}'. Available formats: {available}.");
    }

    /// <inheritdoc/>
    public bool TryForFormat(string format, [NotNullWhen(true)] out IMigrationReporter? reporter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);

        // Last-wins: a later registration for the same format shadows earlier ones.
        for (var i = _reporters.Count - 1; i >= 0; i--)
        {
            if (string.Equals(_reporters[i].Format, format, StringComparison.OrdinalIgnoreCase))
            {
                reporter = _reporters[i];
                return true;
            }
        }

        reporter = null;
        return false;
    }
}
