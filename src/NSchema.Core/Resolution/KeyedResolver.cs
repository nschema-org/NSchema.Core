using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace NSchema.Resolution;

/// <summary>
/// Resolves keyed DI service registrations for <typeparamref name="TValue"/> by string key.
/// </summary>
internal sealed class KeyedResolver<TValue, TOptions> : IKeyedResolver<TValue> where TOptions : class
{
    private readonly Lazy<TValue?> _current;
    private readonly IServiceProvider _provider;
    private readonly Func<TOptions, string?>? _selector;

    /// <summary>
    /// Resolves keyed DI service registrations for <typeparamref name="TValue"/> by string key.
    /// </summary>
    public KeyedResolver(IServiceProvider provider, Func<TOptions, string?>? selector = null)
    {
        _provider = provider;
        _selector = selector;
        _current = new Lazy<TValue?>(GetCurrent);
    }

    public TValue Current => _current.Value ?? throw new InvalidOperationException($"No current {typeof(TValue).Name} is configured.");
    public bool HasCurrent => _current.Value is not null;

    public TValue Resolve(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var result = _provider.GetKeyedService<TValue>(key);
        return result ?? throw new InvalidOperationException($"No {typeof(TValue).Name} registered for '{key}'.");
    }

    public bool TryResolve(string key, [NotNullWhen(true)] out TValue? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        value = _provider.GetKeyedService<TValue>(key);
        return value is not null;
    }

    private TValue? GetCurrent()
    {
        if (_selector is null)
        {
            return default;
        }

        var key = _selector(_provider.GetRequiredService<IOptions<TOptions>>().Value);
        if (string.IsNullOrWhiteSpace(key))
        {
            return default;
        }

        return TryResolve(key, out var value) ? value : default;
    }
}
