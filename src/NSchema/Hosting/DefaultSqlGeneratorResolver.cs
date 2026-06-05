using Microsoft.Extensions.Options;
using NSchema.Resolution;
using NSchema.Sql;

namespace NSchema.Hosting;

/// <summary>
/// Resolves <see cref="ISqlGenerator"/>s from the set registered in DI, by dialect, and selects the one for
/// the run via <see cref="Current"/>.
/// </summary>
internal sealed class DefaultSqlGeneratorResolver(IOptions<MigrationRunOptions> options, IEnumerable<ISqlGenerator> generators)
    : KeyedResolver<string, ISqlGenerator>(generators, g => g.Dialect, "SQL generator", StringComparer.OrdinalIgnoreCase),
      ISqlGeneratorResolver
{
    public IReadOnlyCollection<string> AvailableDialects => Keys;

    public ISqlGenerator? Current
    {
        get
        {
            var dialect = options.Value.Dialect;

            // An explicit dialect must resolve to a registered generator (or fail clearly).
            if (!string.IsNullOrWhiteSpace(dialect))
            {
                return Resolve(dialect);
            }

            // No dialect chosen: preserve the optional/single-generator behaviour. With several registered
            // the choice is ambiguous, so ask the caller to pick one.
            return Keys.Count switch
            {
                0 => null,
                1 => Resolve(Keys.First()),
                _ => throw new InvalidOperationException(
                    $"Multiple SQL dialects are registered ({string.Join(", ", Keys.Order(StringComparer.OrdinalIgnoreCase))}). " +
                    "Choose one with WithDialect(...).")
            };
        }
    }

    public ISqlGenerator ForDialect(string dialect)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dialect);
        return Resolve(dialect);
    }

    public bool TryForDialect(string dialect, out ISqlGenerator? generator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dialect);
        return TryResolve(dialect, out generator);
    }
}
