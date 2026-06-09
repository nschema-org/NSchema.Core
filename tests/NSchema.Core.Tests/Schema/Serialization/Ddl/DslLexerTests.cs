using NSchema.Schema.Serialization.Ddl;

namespace NSchema.Tests.Schema.Serialization.Ddl;

public sealed class DslLexerTests
{
    private static List<Token> Tokens(string source)
    {
        var lexer = new DslLexer(source);
        var tokens = new List<Token>();
        while (true)
        {
            var token = lexer.Next();
            if (token.Kind == TokenKind.EndOfFile)
            {
                return tokens;
            }
            tokens.Add(token);
        }
    }

    private static List<TokenKind> Kinds(string source) => Tokens(source).Select(t => t.Kind).ToList();

    // -------------------------------------------------------------------------
    // Punctuation, identifiers, literals
    // -------------------------------------------------------------------------

    [Fact]
    public void Lex_Punctuation_ProducesSymbolTokens()
    {
        Kinds("(){},;.=").ShouldBe(
        [
            TokenKind.LeftParen, TokenKind.RightParen, TokenKind.LeftBrace, TokenKind.RightBrace,
            TokenKind.Comma, TokenKind.Semicolon, TokenKind.Dot, TokenKind.Equals,
        ]);
    }

    [Fact]
    public void Lex_Identifier_PreservesCase()
    {
        var token = Tokens("Users_42").ShouldHaveSingleItem();
        token.Kind.ShouldBe(TokenKind.Identifier);
        token.Text.ShouldBe("Users_42");
    }

    [Fact]
    public void Lex_QualifiedName_IsIdentifierDotIdentifier()
    {
        Tokens("app.users").Select(t => (t.Kind, t.Text)).ShouldBe(
            [(TokenKind.Identifier, "app"), (TokenKind.Dot, "."), (TokenKind.Identifier, "users")]);
    }

    [Theory]
    [InlineData("CREATE", "create", true)]
    [InlineData("create", "CREATE", true)]
    [InlineData("CREATE", "table", false)]
    public void IsKeyword_MatchesCaseInsensitively(string text, string keyword, bool expected)
        => Tokens(text).ShouldHaveSingleItem().IsKeyword(keyword).ShouldBe(expected);

    [Fact]
    public void IsKeyword_OnNonIdentifier_IsFalse()
        => Tokens("(").ShouldHaveSingleItem().IsKeyword("CREATE").ShouldBeFalse();

    [Fact]
    public void Lex_String_ReturnsUnescapedValueWithoutQuotes()
    {
        var token = Tokens("'hello world'").ShouldHaveSingleItem();
        token.Kind.ShouldBe(TokenKind.String);
        token.Text.ShouldBe("hello world");
    }

    [Fact]
    public void Lex_String_UnescapesDoubledQuotes()
        => Tokens("'it''s here'").ShouldHaveSingleItem().Text.ShouldBe("it's here");

    [Fact]
    public void Lex_UnterminatedString_Throws()
    {
        var ex = Should.Throw<DslSyntaxException>(() => Tokens("'oops"));
        ex.Message.ShouldContain("Unterminated string");
    }

    [Fact]
    public void Lex_Integer_ReturnsDigits()
    {
        var token = Tokens("1024").ShouldHaveSingleItem();
        token.Kind.ShouldBe(TokenKind.Integer);
        token.Text.ShouldBe("1024");
    }

    [Fact]
    public void Lex_UnexpectedCharacter_ThrowsWithPosition()
    {
        var ex = Should.Throw<DslSyntaxException>(() => Tokens("a > b"));
        ex.Message.ShouldContain("Unexpected character '>'");
        ex.Position.Line.ShouldBe(1);
        ex.Position.Column.ShouldBe(3);
    }

    // -------------------------------------------------------------------------
    // Comments
    // -------------------------------------------------------------------------

    [Fact]
    public void Lex_SourceLineComment_IsSkipped()
        => Kinds("-- a note\nusers").ShouldBe([TokenKind.Identifier]);

    [Fact]
    public void Lex_SourceBlockComment_IsSkipped()
        => Kinds("a /* mid */ b").ShouldBe([TokenKind.Identifier, TokenKind.Identifier]);

    [Fact]
    public void Lex_EmptyBlockComment_IsSkippedNotTreatedAsDoc()
        => Kinds("a /**/ b").ShouldBe([TokenKind.Identifier, TokenKind.Identifier]);

    [Fact]
    public void Lex_DocLineComment_IsEmittedTrimmed()
    {
        var token = Tokens("---   All users.   \nusers")[0];
        token.Kind.ShouldBe(TokenKind.DocComment);
        token.Text.ShouldBe("All users.");
    }

    [Fact]
    public void Lex_ConsecutiveDocLines_MergeIntoOneComment()
    {
        var tokens = Tokens("--- Line one.\n--- Line two.\nusers");
        tokens[0].Kind.ShouldBe(TokenKind.DocComment);
        tokens[0].Text.ShouldBe("Line one.\nLine two.");
        tokens[1].Text.ShouldBe("users");
    }

    [Fact]
    public void Lex_DocBlockComment_IsEmittedTrimmed()
    {
        var token = Tokens("/** Owning org. */ users")[0];
        token.Kind.ShouldBe(TokenKind.DocComment);
        token.Text.ShouldBe("Owning org.");
    }

    [Fact]
    public void Lex_DocLineThenDeclaration_AttachesNothingButOrdersBefore()
        => Kinds("--- doc\nCREATE").ShouldBe([TokenKind.DocComment, TokenKind.Identifier]);

    [Fact]
    public void Lex_UnterminatedBlockComment_Throws()
        => Should.Throw<DslSyntaxException>(() => Tokens("/* never ends")).Message.ShouldContain("Unterminated block comment");

    [Fact]
    public void Lex_UnterminatedDocBlock_Throws()
        => Should.Throw<DslSyntaxException>(() => Tokens("/** never ends")).Message.ShouldContain("Unterminated doc-comment");

    // -------------------------------------------------------------------------
    // Positions and lookahead
    // -------------------------------------------------------------------------

    [Fact]
    public void Lex_TracksLineAndColumnAcrossNewlines()
    {
        var tokens = Tokens("CREATE\n  users");
        tokens[0].Position.ShouldBe(new SourcePosition(0, 1, 1));
        tokens[1].Position.Line.ShouldBe(2);
        tokens[1].Position.Column.ShouldBe(3);
    }

    [Fact]
    public void Peek_DoesNotConsume()
    {
        var lexer = new DslLexer("a b");
        lexer.Peek().Text.ShouldBe("a");
        lexer.Peek().Text.ShouldBe("a");   // still 'a'
        lexer.Next().Text.ShouldBe("a");
        lexer.Next().Text.ShouldBe("b");
    }

    [Fact]
    public void Next_AtEnd_ReturnsEndOfFileRepeatedly()
    {
        var lexer = new DslLexer("");
        lexer.Next().Kind.ShouldBe(TokenKind.EndOfFile);
        lexer.Next().Kind.ShouldBe(TokenKind.EndOfFile);
    }

    // -------------------------------------------------------------------------
    // Balanced expression capture
    // -------------------------------------------------------------------------

    [Fact]
    public void ReadParenthesizedExpression_ReturnsInnerTextTrimmed()
        => new DslLexer("(  age >= 0  )").ReadParenthesizedExpression().ShouldBe("age >= 0");

    [Fact]
    public void ReadParenthesizedExpression_HandlesNestedParens()
        => new DslLexer("(coalesce(a, b) > 0)").ReadParenthesizedExpression().ShouldBe("coalesce(a, b) > 0");

    [Fact]
    public void ReadParenthesizedExpression_IgnoresParensInsideStrings()
        => new DslLexer("(note = ')notaparen(')").ReadParenthesizedExpression().ShouldBe("note = ')notaparen('");

    [Fact]
    public void ReadParenthesizedExpression_AfterKeyword_CapturesOpaqueSql()
    {
        // Mirrors the parser's use: consume CHECK, then capture the raw predicate.
        var lexer = new DslLexer("CHECK (quantity > 0 AND price IS NOT NULL)");
        lexer.Next().IsKeyword("check").ShouldBeTrue();
        lexer.ReadParenthesizedExpression().ShouldBe("quantity > 0 AND price IS NOT NULL");
    }

    [Fact]
    public void ReadParenthesizedExpression_ResumesTokenStreamAfterClosingParen()
    {
        var lexer = new DslLexer("(a > 0) , next");
        lexer.ReadParenthesizedExpression().ShouldBe("a > 0");
        lexer.Next().Kind.ShouldBe(TokenKind.Comma);
        lexer.Next().Text.ShouldBe("next");
    }

    [Fact]
    public void ReadParenthesizedExpression_MissingOpenParen_Throws()
        => Should.Throw<DslSyntaxException>(() => new DslLexer("age > 0").ReadParenthesizedExpression())
            .Message.ShouldContain("Expected '('");

    [Fact]
    public void ReadParenthesizedExpression_Unterminated_Throws()
        => Should.Throw<DslSyntaxException>(() => new DslLexer("(a > 0").ReadParenthesizedExpression())
            .Message.ShouldContain("Unterminated expression");
}
