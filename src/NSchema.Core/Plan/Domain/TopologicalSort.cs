namespace NSchema.Plan.Domain;

/// <summary>
/// Orders items so that every item appears after the items it depends on. This is the instance-level ordering the
/// linearizer layers on top of its fixed action-type order: where two actions share a type (e.g. two
/// <c>CreateView</c>s) and one reads the other, the type order can't separate them — a dependency sort must.
/// </summary>
/// <remarks>
/// Only dependencies that resolve to another item in the same set produce an edge; a dependency on something
/// outside the set (already present in the database, or simply not part of this plan) is ignored. The sort is
/// stable: independent items keep their original relative order.
/// </remarks>
internal static class TopologicalSort
{
    /// <summary>
    /// Returns <paramref name="items"/> in dependency order (dependencies first).
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The items to order.</param>
    /// <param name="key">Projects an item's identity.</param>
    /// <param name="dependencies">Projects the identities an item depends on.</param>
    /// <param name="comparer">Compares identities (e.g. case-insensitively).</param>
    /// <param name="describe">Renders an item's identity for the cycle error message.</param>
    /// <exception cref="InvalidOperationException">A dependency cycle exists among the items.</exception>
    public static IReadOnlyList<T> Order<T>(
        IReadOnlyList<T> items,
        Func<T, string> key,
        Func<T, IEnumerable<string>> dependencies,
        IEqualityComparer<string> comparer,
        Func<T, string> describe
    )
    {
        if (items.Count <= 1)
        {
            return items;
        }

        var byKey = new Dictionary<string, T>(comparer);
        foreach (var item in items)
        {
            // A duplicate key would make ordering ambiguous; the first declaration wins (callers dedupe upstream).
            byKey.TryAdd(key(item), item);
        }

        var ordered = new List<T>(items.Count);
        var state = new Dictionary<string, Mark>(comparer);

        // Depth-first post-order over the original sequence preserves the input order for independent items.
        foreach (var item in items)
        {
            Visit(item);
        }

        return ordered;

        void Visit(T item)
        {
            var k = key(item);
            if (state.TryGetValue(k, out var mark))
            {
                if (mark == Mark.InProgress)
                {
                    throw new InvalidOperationException(
                        $"Dependency cycle detected involving {describe(item)}. Cyclic definitions cannot be ordered.");
                }
                return; // already emitted
            }

            state[k] = Mark.InProgress;
            foreach (var dependencyKey in dependencies(item))
            {
                if (byKey.TryGetValue(dependencyKey, out var dependency))
                {
                    Visit(dependency);
                }
            }
            state[k] = Mark.Done;
            ordered.Add(item);
        }
    }

    private enum Mark
    {
        InProgress,
        Done,
    }
}
