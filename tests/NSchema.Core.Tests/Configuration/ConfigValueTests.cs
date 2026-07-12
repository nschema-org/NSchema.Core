using NSchema.Project.Ddl.Models.Config;

namespace NSchema.Tests.Configuration;

public sealed class ConfigValueTests
{
    [Fact]
    public void OfString_RoundTrips()
    {
        var value = ConfigValue.OfString("postgres");
        value.Kind.ShouldBe(ConfigValueKind.String);
        value.AsString().ShouldBe("postgres");
    }

    [Fact]
    public void OfIdentifier_IsReadableAsString()
    {
        // An identifier value is a bareword; AsString surfaces it without quotes.
        var value = ConfigValue.OfIdentifier("single");
        value.Kind.ShouldBe(ConfigValueKind.Identifier);
        value.AsString().ShouldBe("single");
    }

    [Fact]
    public void OfInteger_RoundTrips()
        => ConfigValue.OfInteger(-1).AsInteger().ShouldBe(-1);

    [Fact]
    public void OfBoolean_RoundTrips()
        => ConfigValue.OfBoolean(true).AsBoolean().ShouldBeTrue();

    [Fact]
    public void AsInteger_OnNonInteger_Throws()
        => Should.Throw<InvalidOperationException>(() => ConfigValue.OfString("x").AsInteger());

    [Fact]
    public void AsBoolean_OnNonBoolean_Throws()
        => Should.Throw<InvalidOperationException>(() => ConfigValue.OfInteger(1).AsBoolean());

    [Fact]
    public void AsString_OnNonString_Throws()
        => Should.Throw<InvalidOperationException>(() => ConfigValue.OfBoolean(false).AsString());

    [Fact]
    public void Equality_IsStructural()
        => ConfigValue.OfInteger(10).ShouldBe(ConfigValue.OfInteger(10));
}
