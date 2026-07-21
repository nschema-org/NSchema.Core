using NSchema.Plugins.Model.Config;

namespace NSchema.Tests.Plugins;

/// <summary>
/// The attribute-key value object: case-insensitive identity over the written text, and the owner of the
/// snake_case ↔ .NET-name binding convention.
/// </summary>
public sealed class AttributeKeyTests
{
    [Fact]
    public void Equals_IgnoresCase()
        => new AttributeKey("connection_string").ShouldBe(new AttributeKey("Connection_String"));

    [Fact]
    public void Equals_DistinguishesWrittenText()
        => new AttributeKey("connection_string").ShouldNotBe(new AttributeKey("connectionstring"));

    [Fact]
    public void GetHashCode_MatchesCaseInsensitiveEquality()
        => new AttributeKey("ssl").GetHashCode().ShouldBe(new AttributeKey("SSL").GetHashCode());

    [Fact]
    public void Segments_SplitsADottedKey()
        => new AttributeKey("pool.max_size").Segments.ShouldBe([new AttributeKey("pool"), new AttributeKey("max_size")]);

    [Fact]
    public void Segments_UndottedKeyIsItsOwnSingleSegment()
        => new AttributeKey("ssl").Segments.ShouldBe([new AttributeKey("ssl")]);

    [Fact]
    public void Matches_IgnoresUnderscoresAndCase()
        => new AttributeKey("connection_string").Matches("ConnectionString").ShouldBeTrue();

    [Fact]
    public void Matches_RejectsADifferentName()
        => new AttributeKey("connection_string").Matches("ConnectionTimeout").ShouldBeFalse();

    [Fact]
    public void ForProperty_RendersTheSnakeCaseKey()
        => AttributeKey.ForProperty("ConnectionString").Value.ShouldBe("connection_string");

    [Fact]
    public void ToString_RendersTheWrittenText()
        => new AttributeKey("pool.max").ToString().ShouldBe("pool.max");

    [Fact]
    public void ImplicitConversion_WrapsAWrittenKey()
    {
        // Arrange
        AttributeKey key = "connection_string";

        // Assert
        key.ShouldBe(new AttributeKey("connection_string"));
    }
}
