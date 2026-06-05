namespace NSchema.Resolution;

/// <summary>
/// Base class for resolvers that select one of several <typeparamref name="TValue"/> implementations by a key.
/// </summary>
/// <typeparam name="TKey">The type of the item key. Must be non-nullable.</typeparam>
/// <typeparam name="TValue">The service being resolved.</typeparam>
internal abstract class KeyedResolver<TKey, TValue>
    where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _byName;
    private readonly string _itemNoun;

    /// <param name="items">The registered implementations.</param>
    /// <param name="keyOf">Extracts the name key from an implementation.</param>
    /// <param name="itemNoun">A singular noun for the resolved item, used in error messages (e.g. <c>reporter</c>).</param>
    /// <param name="comparer">The key comparer to use. If null, the default will be used, which is case-sensitive for strings.</param>
    /// <exception cref="InvalidOperationException">Two implementations share a key.</exception>
    protected KeyedResolver(IEnumerable<TValue> items, Func<TValue, TKey> keyOf, string itemNoun, IEqualityComparer<TKey>? comparer = null)
    {
        _byName = new Dictionary<TKey, TValue>(comparer);
        _itemNoun = itemNoun;

        foreach (var item in items)
        {
            var name = keyOf(item);
            if (!_byName.TryAdd(name, item))
            {
                throw new InvalidOperationException($"Multiple {itemNoun}s are registered for '{name}'. Each item must have exactly one {itemNoun}.");
            }
        }
    }

    /// <summary>
    /// The distinct item keys.
    /// </summary>
    public IReadOnlyCollection<TKey> Keys => [.. _byName.Keys];

    /// <summary>
    /// Resolves the implementation registered for <paramref name="key"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">No implementation is registered for the key.</exception>
    public TValue Resolve(TKey key)
    {
        if (_byName.TryGetValue(key, out var item))
        {
            return item;
        }

        var available = _byName.Count == 0
            ? "none"
            : string.Join(", ", _byName.Keys.OrderBy(k => k.ToString()));
        throw new InvalidOperationException($"No {_itemNoun} registered for format '{key}'. Available formats: {available}.");
    }

    /// <summary>
    /// Attempts to resolve the implementation registered for <paramref name="key"/>.
    /// </summary>
    public bool TryResolve(TKey key, out TValue? item)
    {
        return _byName.TryGetValue(key, out item);
    }
}
