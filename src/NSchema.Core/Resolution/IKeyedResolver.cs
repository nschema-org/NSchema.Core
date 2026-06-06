using System.Diagnostics.CodeAnalysis;

namespace NSchema.Resolution;

/// <summary>
/// Resolves one of several <typeparamref name="TValue"/> implementations by a string key.
/// </summary>
public interface IKeyedResolver<TValue>
{
    /// <summary>
    /// Gets the configured default value, or throws if there isn't one configured.
    /// </summary>
    public TValue Current { get; }

    /// <summary>
    /// Indicates whether a default value is configured for <see cref="Current"/>.
    /// If <see langword="false"/>, attempting to access <see cref="Current"/> will throw.
    /// </summary>
    bool HasCurrent { get; }

    /// <summary>
    /// Resolves the implementation registered for <paramref name="key"/> (case-insensitive).
    /// </summary>
    /// <exception cref="InvalidOperationException">No implementation is registered for the key.</exception>
    TValue Resolve(string key);

    /// <summary>
    /// Attempts to resolve the implementation registered for <paramref name="key"/> (case-insensitive).
    /// </summary>
    bool TryResolve(string key, [NotNullWhen(true)] out TValue? value);
}
