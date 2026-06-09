using System.Text;

namespace NSchema.Schema.Serialization.Ddl;

/// <summary>
/// Scanner for NSchema DDL.
/// </summary>
internal sealed class DslLexer(string source)
{
    private readonly string _source = source ?? throw new ArgumentNullException(nameof(source));
    private int _offset;
    private int _line = 1;
    private int _column = 1;

    private bool AtEnd => _offset >= _source.Length;
    private char Current => AtEnd ? '\0' : _source[_offset];
    private char Peek(int ahead) => _offset + ahead < _source.Length ? _source[_offset + ahead] : '\0';
    private SourcePosition Position => new(_offset, _line, _column);

    /// <summary>
    /// Reads the next structural token, skipping whitespace and source comments.
    /// </summary>
    public Token Next()
    {
        while (true)
        {
            if (AtEnd)
            {
                return new Token(TokenKind.EndOfFile, string.Empty, Position);
            }

            var c = Current;
            if (c is ' ' or '\t' or '\r' or '\n')
            {
                Advance();
                continue;
            }

            if (c == '-' && Peek(1) == '-')
            {
                if (Peek(2) == '-')
                {
                    return ReadDocLine();
                }
                SkipLineComment();
                continue;
            }

            if (c == '/' && Peek(1) == '*')
            {
                // /** … */ is a doc-block, but /**/ is just an empty source block.
                if (Peek(2) == '*' && Peek(3) != '/')
                {
                    return ReadDocBlock();
                }
                SkipBlockComment();
                continue;
            }

            break;
        }

        var pos = Position;
        var ch = Current;
        switch (ch)
        {
            case '(': Advance(); return new Token(TokenKind.LeftParen, "(", pos);
            case ')': Advance(); return new Token(TokenKind.RightParen, ")", pos);
            case '{': Advance(); return new Token(TokenKind.LeftBrace, "{", pos);
            case '}': Advance(); return new Token(TokenKind.RightBrace, "}", pos);
            case ',': Advance(); return new Token(TokenKind.Comma, ",", pos);
            case ';': Advance(); return new Token(TokenKind.Semicolon, ";", pos);
            case '.': Advance(); return new Token(TokenKind.Dot, ".", pos);
            case '=': Advance(); return new Token(TokenKind.Equals, "=", pos);
            case '\'': return ReadString(pos);
        }

        if (char.IsAsciiDigit(ch))
        {
            return ReadInteger(pos);
        }

        if (IsIdentifierStart(ch))
        {
            return ReadIdentifier(pos);
        }

        throw new DslSyntaxException($"Unexpected character '{ch}'", pos);
    }

    /// <summary>
    /// Returns the next token without consuming it.
    /// </summary>
    public Token Peek()
    {
        var (offset, line, column) = (_offset, _line, _column);
        var token = Next();
        (_offset, _line, _column) = (offset, line, column);
        return token;
    }

    /// <summary>
    /// Captures a parenthesised opaque expression (e.g. a <c>CHECK</c> body or index <c>WHERE</c> predicate) as
    /// raw text. Skips leading whitespace, requires the next character to be <c>(</c>, and returns the inner text
    /// (between the outer parentheses) trimmed. Parentheses inside single-quoted strings do not affect nesting.
    /// </summary>
    public string ReadParenthesizedExpression()
    {
        SkipInlineWhitespace();
        var pos = Position;
        if (Current != '(')
        {
            throw new DslSyntaxException("Expected '(' to begin an expression", pos);
        }

        Advance(); // consume '('
        var start = _offset;
        var depth = 1;
        while (true)
        {
            if (AtEnd)
            {
                throw new DslSyntaxException("Unterminated expression", pos);
            }

            var c = Current;
            if (c == '\'')
            {
                ConsumeStringLiteral(pos);
                continue;
            }

            if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
                if (depth == 0)
                {
                    var inner = _source[start.._offset];
                    Advance(); // consume ')'
                    return inner.Trim();
                }
            }

            Advance();
        }
    }

    /// <summary>
    /// Captures an unparenthesised <c>DEFAULT</c> expression as raw text, preserving any parentheses the author
    /// wrote. Tracks paren depth (so a comma inside <c>coalesce(a, b)</c> is not a terminator) and string literals,
    /// and stops — without consuming the terminator — at a depth-0 <c>,</c> or <c>)</c> (the enclosing column
    /// list) or the <c>RENAMED</c> keyword.
    /// </summary>
    public string ReadDefaultExpression()
    {
        SkipInlineWhitespace();
        var pos = Position;
        var start = _offset;
        var depth = 0;
        while (!AtEnd)
        {
            var c = Current;
            if (c == '\'')
            {
                ConsumeStringLiteral(pos);
                continue;
            }

            if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                if (depth == 0)
                {
                    break;
                }
                depth--;
            }
            else if (depth == 0)
            {
                if (c == ',')
                {
                    break;
                }
                if (IsIdentifierStart(c) && AtWordStart() && MatchesKeyword("RENAMED"))
                {
                    break;
                }
            }

            Advance();
        }

        var text = _source[start.._offset].Trim();
        if (text.Length == 0)
        {
            throw new DslSyntaxException("Expected a default expression", pos);
        }
        return text;
    }

    /// <summary>
    /// Rewinds the scanner to a previously-observed position. The parser uses this to re-read a token it had
    /// already pulled as lookahead so that an opaque expression can be captured as raw text from the right offset.
    /// </summary>
    public void ResetTo(SourcePosition position)
    {
        _offset = position.Offset;
        _line = position.Line;
        _column = position.Column;
    }

    private bool AtWordStart() => _offset == 0 || !IsIdentifierPart(_source[_offset - 1]);

    /// <summary>Whether the (upper-cased) keyword sits at the current offset, bounded by a non-identifier character.</summary>
    private bool MatchesKeyword(string keyword)
    {
        if (_offset + keyword.Length > _source.Length)
        {
            return false;
        }
        for (var i = 0; i < keyword.Length; i++)
        {
            if (char.ToUpperInvariant(_source[_offset + i]) != keyword[i])
            {
                return false;
            }
        }
        var after = _offset + keyword.Length;
        return after >= _source.Length || !IsIdentifierPart(_source[after]);
    }

    private Token ReadDocLine()
    {
        var pos = Position;
        var builder = new StringBuilder();
        while (true)
        {
            Advance(); Advance(); Advance(); // consume '---'
            var lineStart = _offset;
            while (!AtEnd && Current != '\n')
            {
                Advance();
            }
            builder.Append(_source[lineStart.._offset].Trim());

            if (!AtEnd)
            {
                Advance(); // consume the newline
            }

            // Merge an immediately-following doc-line (allowing leading indentation) into one comment.
            SkipInlineWhitespace();
            if (Current == '-' && Peek(1) == '-' && Peek(2) == '-')
            {
                builder.Append('\n');
                continue;
            }

            return new Token(TokenKind.DocComment, builder.ToString().Trim(), pos);
        }
    }

    private Token ReadDocBlock()
    {
        var pos = Position;
        Advance(); Advance(); Advance(); // consume '/**'
        var start = _offset;
        while (true)
        {
            if (AtEnd)
            {
                throw new DslSyntaxException("Unterminated doc-comment", pos);
            }
            if (Current == '*' && Peek(1) == '/')
            {
                var body = _source[start.._offset];
                Advance(); Advance(); // consume '*/'
                return new Token(TokenKind.DocComment, body.Trim(), pos);
            }
            Advance();
        }
    }

    private Token ReadString(SourcePosition pos)
    {
        Advance(); // consume opening quote
        var builder = new StringBuilder();
        while (true)
        {
            if (AtEnd)
            {
                throw new DslSyntaxException("Unterminated string literal", pos);
            }

            var c = Current;
            if (c == '\'')
            {
                if (Peek(1) == '\'')
                {
                    builder.Append('\'');
                    Advance(); Advance();
                    continue;
                }

                Advance(); // consume closing quote
                return new Token(TokenKind.String, builder.ToString(), pos);
            }

            builder.Append(c);
            Advance();
        }
    }

    private Token ReadInteger(SourcePosition pos)
    {
        var start = _offset;
        while (!AtEnd && char.IsAsciiDigit(Current))
        {
            Advance();
        }
        return new Token(TokenKind.Integer, _source[start.._offset], pos);
    }

    private Token ReadIdentifier(SourcePosition pos)
    {
        var start = _offset;
        while (!AtEnd && IsIdentifierPart(Current))
        {
            Advance();
        }
        return new Token(TokenKind.Identifier, _source[start.._offset], pos);
    }

    /// <summary>
    /// Consumes a single-quoted literal during raw expression capture (so its parens are ignored).
    /// </summary>
    private void ConsumeStringLiteral(SourcePosition exprStart)
    {
        Advance(); // opening quote
        while (true)
        {
            if (AtEnd)
            {
                throw new DslSyntaxException("Unterminated string literal in expression", exprStart);
            }
            if (Current == '\'')
            {
                if (Peek(1) == '\'')
                {
                    Advance(); Advance();
                    continue;
                }
                Advance(); // closing quote
                return;
            }
            Advance();
        }
    }

    private void SkipLineComment()
    {
        while (!AtEnd && Current != '\n')
        {
            Advance();
        }
    }

    private void SkipBlockComment()
    {
        var pos = Position;
        Advance(); Advance(); // consume '/*'
        while (true)
        {
            if (AtEnd)
            {
                throw new DslSyntaxException("Unterminated block comment", pos);
            }
            if (Current == '*' && Peek(1) == '/')
            {
                Advance(); Advance();
                return;
            }
            Advance();
        }
    }

    private void SkipInlineWhitespace()
    {
        while (!AtEnd && Current is ' ' or '\t' or '\r')
        {
            Advance();
        }
    }

    private char Advance()
    {
        var c = _source[_offset++];
        if (c == '\n')
        {
            _line++;
            _column = 1;
        }
        else
        {
            _column++;
        }
        return c;
    }

    private static bool IsIdentifierStart(char c) => char.IsAsciiLetter(c) || c == '_';
    private static bool IsIdentifierPart(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';
}
