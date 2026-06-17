using System.Text;

namespace NSchema.Schema.Serialization.Ddl;

/// <summary>
/// Scanner for NSchema DDL.
/// </summary>
internal sealed class DdlLexer(string source)
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
            case '-': Advance(); return new Token(TokenKind.Minus, "-", pos);
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

        throw new DdlSyntaxException($"Unexpected character '{ch}'", pos);
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
    /// Captures a parenthesized opaque expression (e.g. a <c>CHECK</c> body or index <c>WHERE</c> predicate, or a
    /// routine's argument list) as raw text. Skips leading whitespace, requires and consumes the surrounding
    /// parentheses, and returns the inner text trimmed — which may be empty (e.g. an empty argument list). The scan
    /// itself delegates to <see cref="ReadRawSpan"/>, so strings, comments and dollar-quoted sections inside the
    /// expression are honoured and any nested parentheses are balanced.
    /// </summary>
    public string ReadParenthesizedExpression()
    {
        SkipInlineWhitespace();
        var pos = Position;
        if (Current != '(')
        {
            throw new DdlSyntaxException("Expected '(' to begin an expression", pos);
        }

        Advance(); // consume '('
        var inner = ReadRawSpan("an expression", ")", allowEmpty: true);
        if (AtEnd)
        {
            // The scan reached end-of-source without finding the matching depth-zero ')'.
            throw new DdlSyntaxException("Unterminated expression", pos);
        }

        Advance(); // consume ')'
        return inner;
    }

    /// <summary>
    /// Captures a raw, opaque span of source as verbatim text.
    /// </summary>
    /// <param name="what">What the span is, for the empty-span error (e.g. "a view body", "a default expression").</param>
    /// <param name="terminators">The depth-zero characters that end the span (e.g. <c>";"</c>, or <c>",)"</c>).</param>
    /// <param name="terminatorKeyword">An optional upper-cased keyword that also ends the span at a word boundary.</param>
    /// <param name="allowEmpty">When <see langword="true"/>, an empty span is returned as-is instead of throwing (e.g. an empty argument list).</param>
    public string ReadRawSpan(string what, string terminators, string? terminatorKeyword = null, bool allowEmpty = false)
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

            if (c == '$' && TryConsumeDollarQuote(pos))
            {
                continue;
            }

            // Consume comments verbatim so a terminator inside them is not treated as structural.
            if (c == '-' && Peek(1) == '-')
            {
                SkipLineComment();
                continue;
            }
            if (c == '/' && Peek(1) == '*')
            {
                SkipBlockComment();
                continue;
            }

            if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                if (depth > 0)
                {
                    depth--;
                }
                else if (terminators.Contains(')'))
                {
                    break;
                }
            }
            else if (depth == 0)
            {
                if (terminators.Contains(c))
                {
                    break;
                }
                if (terminatorKeyword is not null && IsIdentifierStart(c) && AtWordStart() && MatchesKeyword(terminatorKeyword))
                {
                    break;
                }
            }

            Advance();
        }

        var text = _source[start.._offset].Trim();
        if (!allowEmpty && text.Length == 0)
        {
            throw new DdlSyntaxException($"Expected {what}", pos);
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

    /// <summary>
    /// Consumes a dollar-quoted string (<c>$$…$$</c> or <c>$tag$…$tag$</c>, Postgres tag rules: empty, or an
    /// identifier) starting at the current <c>$</c>, including its closing tag. Returns <see langword="false"/>
    /// without consuming anything when the <c>$</c> does not open a valid tag (e.g. <c>$1</c> or a lone
    /// <c>$</c>) — the caller treats it as ordinary text. A differently-tagged quote inside is just content;
    /// only the opening tag closes the string.
    /// </summary>
    private bool TryConsumeDollarQuote(SourcePosition statementStart)
    {
        var i = 1;
        if (IsIdentifierStart(Peek(1)))
        {
            i++;
            while (IsIdentifierPart(Peek(i)))
            {
                i++;
            }
        }

        if (Peek(i) != '$')
        {
            return false;
        }

        var tag = _source.Substring(_offset, i + 1);
        for (var j = 0; j < tag.Length; j++)
        {
            Advance();
        }

        while (!AtEnd)
        {
            if (Current == '$' && _offset + tag.Length <= _source.Length
                && string.CompareOrdinal(_source, _offset, tag, 0, tag.Length) == 0)
            {
                for (var j = 0; j < tag.Length; j++)
                {
                    Advance();
                }
                return true;
            }

            Advance();
        }

        throw new DdlSyntaxException("Unterminated dollar-quoted string", statementStart);
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
                throw new DdlSyntaxException("Unterminated doc-comment", pos);
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
                throw new DdlSyntaxException("Unterminated string literal", pos);
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
                throw new DdlSyntaxException("Unterminated string literal in expression", exprStart);
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
                throw new DdlSyntaxException("Unterminated block comment", pos);
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
