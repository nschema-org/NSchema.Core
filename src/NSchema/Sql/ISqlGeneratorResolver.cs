namespace NSchema.Sql;

/// <summary>
/// Resolves a registered <see cref="ISqlGenerator"/> by its dialect, and selects the one for the current run via <see cref="Current"/>.
/// </summary>
public interface ISqlGeneratorResolver
{
    /// <summary>
    /// The distinct dialects that can be resolved, e.g. <c>postgres</c>, <c>mysql</c>.
    /// </summary>
    IReadOnlyCollection<string> AvailableDialects { get; }

    /// <summary>
    /// The generator for the run: the one matching the run's configured dialect when set; otherwise the
    /// single registered generator, or <see langword="null"/> if none is registered.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A dialect is configured but not registered, or several are registered without one being chosen.
    /// </exception>
    ISqlGenerator? Current { get; }

    /// <summary>
    /// Resolves the generator registered for <paramref name="dialect"/> (case-insensitive).
    /// </summary>
    /// <param name="dialect">The dialect, e.g. <c>postgres</c>.</param>
    /// <exception cref="InvalidOperationException">No generator is registered for the dialect.</exception>
    ISqlGenerator ForDialect(string dialect);

    /// <summary>
    /// Attempts to resolve the generator registered for <paramref name="dialect"/> (case-insensitive).
    /// </summary>
    /// <param name="dialect">The dialect, e.g. <c>postgres</c>.</param>
    /// <param name="generator">The resolved generator, or <see langword="null"/> if none is registered.</param>
    /// <returns><see langword="true"/> if a generator was found; otherwise <see langword="false"/>.</returns>
    bool TryForDialect(string dialect, out ISqlGenerator? generator);
}
