using NSchema.Diff.Domain;
using NSchema.Model;

namespace NSchema.Tests.Diff;

public sealed class SqlTextNormalizerTests
{
    [Theory]
    [InlineData("SELECT id FROM app.users", "SELECT  id   FROM\tapp.users")]            // collapsed whitespace
    [InlineData("SELECT id FROM app.users", "SELECT id\n  FROM app.users")]              // newlines + indent
    [InlineData("SELECT id FROM app.users", "  SELECT id FROM app.users  ")]             // leading/trailing trim
    [InlineData("SELECT * FROM app.users", "SELECT * FROM app.users;")]                  // trailing terminator
    [InlineData("SELECT * FROM app.users", "SELECT * FROM app.users ;")]                 // terminator after space
    [InlineData("SELECT 'a  b' AS l", "SELECT  'a  b'  AS  l")]                          // literal kept, gaps collapsed
    public void AreEquivalent_CosmeticDifferences_AreEqual(string current, string desired)
        => SqlTextNormalizer.AreEquivalent(new SqlText(current), new SqlText(desired)).ShouldBeTrue();

    [Theory]
    [InlineData("SELECT 'a  b'", "SELECT 'a b'")]                                        // whitespace inside literal
    [InlineData("SELECT 'It''s' ", "SELECT 'Its'")]                                      // doubled-quote escape differs
    [InlineData("SELECT \"a  b\"", "SELECT \"a b\"")]                                    // quoted identifier preserved
    [InlineData("SELECT id FROM app.users", "SELECT id FROM app.members")]               // genuinely different
    [InlineData("SELECT a;b", "SELECT a")]                                               // interior ';' is significant
    public void AreEquivalent_SignificantDifferences_AreNotEqual(string current, string desired)
        => SqlTextNormalizer.AreEquivalent(new SqlText(current), new SqlText(desired)).ShouldBeFalse();

    [Fact]
    public void AreEquivalent_IsReflexive_ForBodyWithEmbeddedQuotesAndSemicolon()
        => SqlTextNormalizer.AreEquivalent(new SqlText("SELECT 'a;b', \"Col\" FROM app.t"), new SqlText("SELECT 'a;b',  \"Col\"  FROM app.t")).ShouldBeTrue();
}
