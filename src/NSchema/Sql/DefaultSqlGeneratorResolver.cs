using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;
using NSchema.Hosting;
using NSchema.Resolution;

namespace NSchema.Sql;

/// <summary>
/// Resolves <see cref="ISqlGenerator"/>s from the set registered in DI, by dialect, and selects the one for
/// the run via <see cref="Current"/>.
/// </summary>
internal sealed class DefaultSqlGeneratorResolver(IOptions<MigrationRunOptions> options, IEnumerable<ISqlGenerator> generators)
    : KeyedResolver<string, ISqlGenerator>(generators, g => g.Dialect, "SQL generator", StringComparer.OrdinalIgnoreCase),
      ISqlGeneratorResolver
{
    public IReadOnlyCollection<string> AvailableDialects => Keys;

    public ISqlGenerator Current
    {
        get => TryForDialect(options.Value.Dialect, out var result) ? result : throw new InvalidOperationException("No sql generator configured.");
    }

    public ISqlGenerator ForDialect(string dialect)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dialect);
        return Resolve(dialect);
    }

    public bool TryForDialect(string dialect, [NotNullWhen(true)] out ISqlGenerator? generator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dialect);
        return TryResolve(dialect, out generator);
    }
}
