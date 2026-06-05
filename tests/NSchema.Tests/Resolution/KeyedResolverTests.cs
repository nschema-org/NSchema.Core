using NSchema.Resolution;

namespace NSchema.Tests.Resolution;

/// <summary>
/// Tests the shared resolution behaviour for every <see cref="KeyedResolver{TKey,TValue}"/> subclass
/// (reporters, SQL generators, schema serializers), so the concrete resolvers only need to cover their own
/// selection logic on top.
/// </summary>
public sealed class KeyedResolverTests
{
    private sealed record Item(string Key, string Value);

    /// <summary>A minimal concrete resolver that exposes the protected base behaviour for testing.</summary>
    private sealed class ItemResolver(IEnumerable<Item> items)
        : KeyedResolver<string, Item>(items, i => i.Key, "item", StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Resolve_ReturnsItem_ForRegisteredKey()
    {
        var json = new Item("json", "j");
        var sut = new ItemResolver([json, new Item("yaml", "y")]);

        sut.Resolve("json").ShouldBeSameAs(json);
    }

    [Fact]
    public void Resolve_IsCaseInsensitive()
    {
        var json = new Item("json", "j");
        var sut = new ItemResolver([json]);

        sut.Resolve("JSON").ShouldBeSameAs(json);
    }

    [Fact]
    public void Resolve_LastRegistrationWins_ForDuplicateKey()
    {
        var first = new Item("json", "first");
        var second = new Item("JSON", "second");
        var sut = new ItemResolver([first, second]);

        // A later registration replaces an earlier one for the same key, letting callers override defaults.
        sut.Resolve("json").ShouldBeSameAs(second);
    }

    [Fact]
    public void Resolve_UnknownKey_ThrowsListingAvailableKeys()
    {
        var sut = new ItemResolver([new Item("json", "j"), new Item("yaml", "y")]);

        var ex = Should.Throw<InvalidOperationException>(() => sut.Resolve("xml"));

        ex.Message.ShouldContain("xml");
        ex.Message.ShouldContain("json");
        ex.Message.ShouldContain("yaml");
    }

    [Fact]
    public void Resolve_WhenNoneRegistered_ThrowsSayingNone()
    {
        var sut = new ItemResolver([]);

        var ex = Should.Throw<InvalidOperationException>(() => sut.Resolve("json"));

        ex.Message.ShouldContain("none");
    }

    [Fact]
    public void TryResolve_ReturnsTrueAndItem_WhenRegistered()
    {
        var json = new Item("json", "j");
        var sut = new ItemResolver([json]);

        sut.TryResolve("json", out var item).ShouldBeTrue();
        item.ShouldBeSameAs(json);
    }

    [Fact]
    public void TryResolve_ReturnsFalseAndNull_WhenAbsent()
    {
        var sut = new ItemResolver([new Item("json", "j")]);

        sut.TryResolve("xml", out var item).ShouldBeFalse();
        item.ShouldBeNull();
    }

    [Fact]
    public void Keys_ListsDistinctRegisteredKeys()
    {
        var sut = new ItemResolver([new Item("json", "a"), new Item("yaml", "b"), new Item("json", "c")]);

        sut.Keys.ShouldBe(["json", "yaml"], ignoreOrder: true);
    }
}
