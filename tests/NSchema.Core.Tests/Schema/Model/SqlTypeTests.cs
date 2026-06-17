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

    [Theory]
    // The DDL is provider-agnostic, so common SQL spellings are accepted as aliases of the canonical type. Aliasing here
    // (rather than preserving them as Custom types) keeps SQL-spelt schemas from drifting against introspection,
    // which always reports the canonical type.
    [InlineData("bool", "boolean")]
    [InlineData("integer", "int")]
    [InlineData("INTEGER", "int")]
    [InlineData("int2", "smallint")]
    [InlineData("int4", "int")]
    [InlineData("int8", "bigint")]
    [InlineData("real", "float")]
    [InlineData("float4", "float")]
    [InlineData("float8", "double")]
    [InlineData("timestamp", "datetime")]
    [InlineData("timestamptz", "datetimeoffset")]
    [InlineData("uuid", "guid")]
    [InlineData("bytea", "varbinary")]
    public void Parse_SqlSpellingAlias_NormalizesToCanonical(string input, string canonical)
    {
        var parsed = SqlType.Parse(input);

        parsed.ShouldBe(SqlType.Parse(canonical));
        parsed.ToString().ShouldBe(canonical);
    }

    [Theory]
    [InlineData("numeric(10, 2)", "decimal(10,2)")]
    [InlineData("character(8)", "char(8)")]
    public void Parse_ParameterisedSqlSpellingAlias_NormalizesToCanonical(string input, string canonical)
    {
        SqlType.Parse(input).ToString().ShouldBe(canonical);
    }
}
