using Microsoft.Extensions.Options;
using NSchema.Migration;
using NSchema.Resolution;

namespace NSchema.Hosting;

/// <summary>
/// Resolves <see cref="IMigrationReporter"/>s from the registered candidates.
/// </summary>
internal sealed class DefaultMigrationReporterResolver(IOptions<MigrationRunOptions> options, IEnumerable<IMigrationReporter> reporters)
    : KeyedResolver<string, IMigrationReporter>(reporters, r => r.Format, "reporter", StringComparer.OrdinalIgnoreCase),
      IMigrationReporterResolver
{
    public IReadOnlyCollection<string> AvailableFormats => Keys;

    public IMigrationReporter Current => Resolve(options.Value.OutputFormat);

    public IMigrationReporter ForFormat(string format) => Resolve(format);

    public bool TryForFormat(string format, out IMigrationReporter? reporter) => TryResolve(format, out reporter);
}
