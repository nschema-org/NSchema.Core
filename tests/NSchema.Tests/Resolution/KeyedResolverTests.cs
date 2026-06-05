using Microsoft.Extensions.DependencyInjection;
using NSchema.Resolution;

namespace NSchema.Tests.Resolution;

/// <summary>
/// Tests <see cref="DefaultKeyedResolver{TValue,TOptions}"/> — the shared resolver backing every keyed-service seam.
/// </summary>
public sealed class KeyedResolverTests
{
    private sealed class Options
    {
        public string? Key { get; set; }
    }

    private static (IKeyedResolver<string> resolver, IServiceProvider provider) Build(
        Action<IServiceCollection>? configure = null,
        string? selectedKey = null)
    {
        var services = new ServiceCollection();
        configure?.Invoke(services);
        services.Configure<Options>(o => o.Key = selectedKey);
        services.AddSingleton<IKeyedResolver<string>>(sp =>
            new DefaultKeyedResolver<string, Options>(sp, o => o.Key));
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IKeyedResolver<string>>(), provider);
    }

    [Fact]
    public void Resolve_ReturnsRegisteredValue()
    {
        var (sut, _) = Build(s => s.AddKeyedSingleton<string>("json", (_, _) => "json-value"));

        sut.Resolve("json").ShouldBe("json-value");
    }

    [Fact]
    public void Resolve_UnknownKey_Throws()
    {
        var (sut, _) = Build(s =>
        {
            s.AddKeyedSingleton<string>("json", (_, _) => "j");
            s.AddKeyedSingleton<string>("yaml", (_, _) => "y");
        });

        var ex = Should.Throw<InvalidOperationException>(() => sut.Resolve("xml"));

        ex.Message.ShouldContain("xml");
    }

    [Fact]
    public void TryResolve_ReturnsTrueAndValue_WhenPresent()
    {
        var (sut, _) = Build(s => s.AddKeyedSingleton<string>("json", (_, _) => "json-value"));

        sut.TryResolve("json", out var value).ShouldBeTrue();
        value.ShouldBe("json-value");
    }

    [Fact]
    public void TryResolve_ReturnsFalseAndNull_WhenAbsent()
    {
        var (sut, _) = Build(s => s.AddKeyedSingleton<string>("json", (_, _) => "j"));

        sut.TryResolve("xml", out var value).ShouldBeFalse();
        value.ShouldBeNull();
    }

    [Fact]
    public void Current_ReturnsValue_WhenKeyConfiguredAndRegistered()
    {
        var (sut, _) = Build(
            s => s.AddKeyedSingleton<string>("json", (_, _) => "json-value"),
            selectedKey: "json");

        sut.HasCurrent.ShouldBeTrue();
        sut.Current.ShouldBe("json-value");
    }

    [Fact]
    public void Current_Throws_WhenKeyNotConfigured()
    {
        var (sut, _) = Build(s => s.AddKeyedSingleton<string>("json", (_, _) => "j"));

        sut.HasCurrent.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => sut.Current);
    }

    [Fact]
    public void Current_Throws_WhenKeyConfiguredButNotRegistered()
    {
        var (sut, _) = Build(selectedKey: "xml");

        sut.HasCurrent.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => sut.Current);
    }

    [Fact]
    public void HasCurrent_False_WhenNoSelector()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<string>("json", (_, _) => "j");
        services.AddSingleton<IKeyedResolver<string>>(sp =>
            new DefaultKeyedResolver<string, Options>(sp, selector: null));
        var sut = services.BuildServiceProvider().GetRequiredService<IKeyedResolver<string>>();

        sut.HasCurrent.ShouldBeFalse();
    }
}
