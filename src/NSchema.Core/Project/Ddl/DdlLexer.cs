using System.Text;
using NSchema.Project.Ddl.Models;
using NSchema.Project.Nsql;

namespace NSchema.Project.Ddl;

/// <summary>
/// Scanner for NSchema DDL.
/// </summary>
internal sealed class DdlLexer(string source, bool emitComments = false)
{
    private readonly string _source = source ?? throw new ArgumentNullException(nameof(source));

    // When true (the formatter), source comments are returned as LineComment/BlockComment tokens instead of skipped.
    // The parser leaves this false, so its token stream is unchanged.
    private int _offset;
    private int _line = 1;
    private int _column = 1;

    private bool AtEnd => _offset >= _source.Length;
    private char Current => AtEnd ? '\0' : _source[_offset];
    private char Peek(int ahead) => _offset + ahead < _source.Length ? _source[_offset + ahead] : '\0';
    private SourcePosition Position => new(_offset, _line, _column);

    /// <summary>
    /// Returns the verbatim source between two offsets — the parser's hook for capturing opaque spans.
    /// </summary>
    public string Slice(int start, int end) => _source[start..end];

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
                if (emitComments)
                {
                    return ReadLineComment();
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
                if (emitComments)
                {
                    return ReadBlockComment();
                }
                SkipBlockComment();
                continue;
            }

            break;
        }

        var pos = Position;
        var ch = Current;

        // A dollar-quote ($$ … $$ or $tag$ … $tag$) is lexed whole; a bare '$' that opens no valid tag (e.g. $1)
        // falls through to the Symbol catch-all below.
        if (ch == '$' && PeekDollarTag() is { } tag)
        {
            return ReadDollarString(pos, tag);
        }

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

        // Any other character (an operator inside an opaque expression) is a single Symbol token. The lexer never
        // rejects it — the parser slices such expressions from the source rather than interpreting their tokens.
        Advance();
        return new Token(TokenKind.Symbol, ch.ToString(), pos);
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
    /// Lexes a dollar-quoted string (<c>$$…$$</c> or <c>$tag$…$tag$</c>) starting at the current <c>$</c>, including
    /// both delimiters, as a single token whose text is the verbatim source. A differently-tagged quote inside is
    /// just content; only the opening tag closes the string.
    /// </summary>
    private Token ReadDollarString(SourcePosition pos, string tag)
    {
        var start = _offset;
        ConsumeLength(tag.Length); // opening tag
        while (!AtEnd)
        {
            if (Current == '$' && MatchesAt(tag))
            {
                ConsumeLength(tag.Length); // closing tag
                return new Token(TokenKind.DollarString, _source[start.._offset], pos);
            }
            Advance();
        }

        throw new DdlSyntaxException("Unterminated dollar-quoted string", pos);
    }

    /// <summary>
    /// Returns the dollar-quote tag (<c>$$</c> or <c>$tag$</c>) opening at the current offset, or <see langword="null"/>
    /// when the <c>$</c> does not open a valid tag (Postgres rules: empty, or an identifier).
    /// </summary>
    private string? PeekDollarTag()
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

        return Peek(i) == '$' ? _source.Substring(_offset, i + 1) : null;
    }

    /// <summary>Whether <paramref name="text"/> sits verbatim at the current offset.</summary>
    private bool MatchesAt(string text) =>
        _offset + text.Length <= _source.Length && string.CompareOrdinal(_source, _offset, text, 0, text.Length) == 0;

    private void ConsumeLength(int count)
    {
        for (var i = 0; i < count; i++)
        {
            Advance();
        }
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

    private void SkipLineComment()
    {
        while (!AtEnd && Current != '\n')
        {
            Advance();
        }
    }

    private Token ReadLineComment()
    {
        var pos = Position;
        var start = _offset;
        while (!AtEnd && Current != '\n')
        {
            Advance();
        }
        return new Token(TokenKind.LineComment, _source[start.._offset].TrimEnd(), pos);
    }

    private Token ReadBlockComment()
    {
        var pos = Position;
        var start = _offset;
        Advance(); Advance(); // consume '/*'
        while (true)
        {
            if (AtEnd)
            {
                throw new DdlSyntaxException("Unterminated block comment", pos);
            }
            if (Current == '*' && Peek(1) == '/')
            {
                Advance(); Advance(); // consume '*/'
                return new Token(TokenKind.BlockComment, _source[start.._offset], pos);
            }
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

    private void Advance()
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
    }

    private static bool IsIdentifierStart(char c) => char.IsAsciiLetter(c) || c == '_';
    private static bool IsIdentifierPart(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';
}
