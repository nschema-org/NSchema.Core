using NSchema.Schema.Model;

namespace NSchema.Tests.Schema.Model;

public sealed class SqlTypeTests
{
    [Theory]
    [InlineData("citext", "citext")]
    [InlineData("CITEXT", "citext")]
    [InlineData("  CiText  ", "citext")]
    public void Name_IsTrimmedAndLowerCased(string input, string expected)
    {
        SqlType.Custom(input).Name.ShouldBe(expected);
    }

    [Fact]
    public void Equality_IgnoresCaseAndWhitespaceInName()
    {
        SqlType.Custom(" CITEXT ").ShouldBe(SqlType.Custom("citext"));
    }

    [Fact]
    public void Equality_DistinguishesDifferentNames()
    {
        SqlType.Custom("citext").ShouldNotBe(SqlType.Custom("hstore"));
    }

    [Fact]
    public void ToString_UsesNormalizedName()
    {
        SqlType.Custom("  CITEXT  ").ToString().ShouldBe("citext");
    }

    [Fact]
    public void Parse_OfNormalizedName_RoundTripsThroughEquality()
    {
        var original = SqlType.Custom("CiText");

        SqlType.Parse(original.ToString()).ShouldBe(original);
    }
}
