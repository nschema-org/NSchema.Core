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
    public void Lex_LoneMinus_IsAMinusToken()
    {
        // A lone '-' signs a numeric value (sequence options); '--' still starts a comment.
        Kinds("-1").ShouldBe([TokenKind.Minus, TokenKind.Integer]);
        Tokens("-- comment\n-").ShouldHaveSingleItem().Kind.ShouldBe(TokenKind.Minus);
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

    // -------------------------------------------------------------------------
    // ReadStatementBody — dollar-quoted strings (routine definitions)
    // -------------------------------------------------------------------------

    [Fact]
    public void ReadStatementBody_DollarQuote_SwallowsInternalSemicolons()
        => new DslLexer("RETURNS int AS $$ SELECT 1; $$;").ReadRawSpan("a view body", ";")
            .ShouldBe("RETURNS int AS $$ SELECT 1; $$");

    [Fact]
    public void ReadStatementBody_TaggedDollarQuote_MatchesOnlyItsOwnTag()
        // The inner $$ is just content; only the opening $body$ tag closes the string.
        => new DslLexer("AS $body$ SELECT '$$'; $$ ; $body$;").ReadRawSpan("a view body", ";")
            .ShouldBe("AS $body$ SELECT '$$'; $$ ; $body$");

    [Fact]
    public void ReadStatementBody_DollarQuote_ContainingQuotesAndComments()
        => new DslLexer("AS $$ -- don't stop; here\n SELECT 'a;b'; $$;").ReadRawSpan("a view body", ";")
            .ShouldBe("AS $$ -- don't stop; here\n SELECT 'a;b'; $$");

    [Fact]
    public void ReadStatementBody_DollarSignThatIsNotATag_IsOrdinaryText()
        // $1 is a parameter reference, not a dollar-quote tag; the body still ends at the top-level ';'.
        => new DslLexer("AS RETURN $1 + 1;").ReadRawSpan("a view body", ";")
            .ShouldBe("AS RETURN $1 + 1");

    [Fact]
    public void ReadStatementBody_NestedDifferentTags_OuterWins()
        => new DslLexer("AS $outer$ a $inner$ b; $inner$ c $outer$;").ReadRawSpan("a view body", ";")
            .ShouldBe("AS $outer$ a $inner$ b; $inner$ c $outer$");

    [Fact]
    public void ReadStatementBody_UnterminatedDollarQuote_Throws()
        => Should.Throw<DslSyntaxException>(() => new DslLexer("AS $$ SELECT 1;").ReadRawSpan("a view body", ";"))
            .Message.ShouldContain("Unterminated dollar-quoted string");

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

    // -------------------------------------------------------------------------
    // Bare default-expression capture
    // -------------------------------------------------------------------------

    [Fact]
    public void ReadDefaultExpression_Literal_ReturnsTrimmedValue()
        => new DslLexer("  42  ").ReadRawSpan("a default expression", ",)", "RENAMED").ShouldBe("42");

    [Fact]
    public void ReadDefaultExpression_FunctionCall_KeepsItsParens()
        => new DslLexer("now()").ReadRawSpan("a default expression", ",)", "RENAMED").ShouldBe("now()");

    [Fact]
    public void ReadDefaultExpression_CommaInsideParens_IsNotATerminator()
        => new DslLexer("coalesce(a, b)").ReadRawSpan("a default expression", ",)", "RENAMED").ShouldBe("coalesce(a, b)");

    [Fact]
    public void ReadDefaultExpression_ParenthesisedValue_PreservesOuterParens()
        => new DslLexer("(a + b)").ReadRawSpan("a default expression", ",)", "RENAMED").ShouldBe("(a + b)");

    [Fact]
    public void ReadDefaultExpression_StopsAtTopLevelComma_WithoutConsumingIt()
    {
        var lexer = new DslLexer("0, next");
        lexer.ReadRawSpan("a default expression", ",)", "RENAMED").ShouldBe("0");
        lexer.Next().Kind.ShouldBe(TokenKind.Comma);
    }

    [Fact]
    public void ReadDefaultExpression_StopsAtTopLevelCloseParen_WithoutConsumingIt()
    {
        var lexer = new DslLexer("5)");
        lexer.ReadRawSpan("a default expression", ",)", "RENAMED").ShouldBe("5");
        lexer.Next().Kind.ShouldBe(TokenKind.RightParen);
    }

    [Theory]
    [InlineData("0 RENAMED FROM old")]
    [InlineData("0 renamed from old")]
    public void ReadDefaultExpression_StopsAtRenamedKeyword(string source)
    {
        var lexer = new DslLexer(source);
        lexer.ReadRawSpan("a default expression", ",)", "RENAMED").ShouldBe("0");
        lexer.Next().IsKeyword("RENAMED").ShouldBeTrue();
    }

    [Fact]
    public void ReadDefaultExpression_RenamedInsideIdentifier_DoesNotStop()
    {
        // 'RENAMED' is only a terminator at a word boundary — embedded in an identifier it is just text.
        new DslLexer("col_RENAMED_at").ReadRawSpan("a default expression", ",)", "RENAMED").ShouldBe("col_RENAMED_at");
    }

    [Fact]
    public void ReadDefaultExpression_DelimitersInsideString_AreIgnored()
        => new DslLexer("'a, b) RENAMED'").ReadRawSpan("a default expression", ",)", "RENAMED").ShouldBe("'a, b) RENAMED'");

    [Fact]
    public void ReadDefaultExpression_Empty_Throws()
        => Should.Throw<DslSyntaxException>(() => new DslLexer(", rest").ReadRawSpan("a default expression", ",)", "RENAMED"))
            .Message.ShouldContain("Expected a default expression");

    // -------------------------------------------------------------------------
    // Statement-body capture (view definitions)
    // -------------------------------------------------------------------------

    [Fact]
    public void ReadStatementBody_ReturnsTextUpToTerminator_TrimmedWithoutConsumingSemicolon()
    {
        var lexer = new DslLexer("SELECT id FROM app.users ;");
        lexer.ReadRawSpan("a view body", ";").ShouldBe("SELECT id FROM app.users");
        lexer.Next().Kind.ShouldBe(TokenKind.Semicolon);
    }

    [Fact]
    public void ReadStatementBody_SemicolonInsideString_IsNotATerminator()
        => new DslLexer("SELECT ';' AS marker FROM app.t;").ReadRawSpan("a view body", ";")
            .ShouldBe("SELECT ';' AS marker FROM app.t");

    [Fact]
    public void ReadStatementBody_ParensAreBalanced_AndPreserved()
        => new DslLexer("SELECT (a + b) FROM app.t;").ReadRawSpan("a view body", ";")
            .ShouldBe("SELECT (a + b) FROM app.t");

    [Fact]
    public void ReadStatementBody_SemicolonInLineComment_IsNotATerminator()
    {
        var lexer = new DslLexer("SELECT 1 -- a; b\nFROM app.t;");
        lexer.ReadRawSpan("a view body", ";").ShouldBe("SELECT 1 -- a; b\nFROM app.t");
        lexer.Next().Kind.ShouldBe(TokenKind.Semicolon);
    }

    [Fact]
    public void ReadStatementBody_SemicolonInBlockComment_IsNotATerminator()
        => new DslLexer("SELECT 1 /* a; b */ FROM app.t;").ReadRawSpan("a view body", ";")
            .ShouldBe("SELECT 1 /* a; b */ FROM app.t");

    [Fact]
    public void ReadStatementBody_StopsAtTopLevelSemicolon_IgnoringParenthesisedSemicolonless()
    {
        // A nested SELECT inside parens does not terminate the outer body; the first top-level ';' does.
        var lexer = new DslLexer("SELECT * FROM (SELECT id FROM app.inner_t) s; CREATE");
        lexer.ReadRawSpan("a view body", ";").ShouldBe("SELECT * FROM (SELECT id FROM app.inner_t) s");
        lexer.Next().Kind.ShouldBe(TokenKind.Semicolon);
        lexer.Next().IsKeyword("CREATE").ShouldBeTrue();
    }

    [Fact]
    public void ReadStatementBody_Empty_Throws()
        => Should.Throw<DslSyntaxException>(() => new DslLexer(";").ReadRawSpan("a view body", ";"))
            .Message.ShouldContain("Expected a view body");

    [Fact]
    public void ReadStatementBody_AfterResetTo_MirrorsParserUsage()
    {
        // The parser consumes 'AS' then rewinds to its one-token lookahead before capturing the body verbatim.
        var lexer = new DslLexer("AS SELECT id FROM app.users;");
        lexer.Next().IsKeyword("AS").ShouldBeTrue();
        var lookahead = lexer.Next();                 // 'SELECT', pulled as lookahead
        lexer.ResetTo(lookahead.Position);
        lexer.ReadRawSpan("a view body", ";").ShouldBe("SELECT id FROM app.users");
        lexer.Next().Kind.ShouldBe(TokenKind.Semicolon);
    }

    // -------------------------------------------------------------------------
    // ResetTo (rewind / re-read)
    // -------------------------------------------------------------------------

    [Fact]
    public void ResetTo_RewindsToATokenAndReReadsIt()
    {
        var lexer = new DslLexer("alpha beta");
        lexer.Next();
        var second = lexer.Next();
        lexer.ResetTo(second.Position);
        lexer.Next().Text.ShouldBe("beta");
    }

    [Fact]
    public void ResetTo_ThenRawCapture_MirrorsParserUsage()
    {
        // The parser holds a one-token lookahead, so it rewinds to that token before capturing raw expression text.
        var lexer = new DslLexer("now() RENAMED FROM old");
        var firstToken = lexer.Next();          // pulled as lookahead; the scanner is now past 'now'
        lexer.ResetTo(firstToken.Position);     // rewind to where the expression starts
        lexer.ReadRawSpan("a default expression", ",)", "RENAMED").ShouldBe("now()");
        lexer.Next().IsKeyword("RENAMED").ShouldBeTrue();   // stream resumes cleanly at the boundary
    }
}
