using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql;

/// <summary>
/// Recursive-descent parser for the NSchema language, producing the syntax tree.
/// The AST is then assembled into a <see cref="NSchema.Project.Domain.Models.ProjectDefinition"/> model.
/// </summary>
internal sealed partial class NsqlParser
{
    private readonly NsqlLexer _lexer;
    private Token _current;

    // True while parsing a template body, where unqualified names are legal (they bind to the applied
    // schema at projection) and template-restricted statements are rejected.
    private bool _inTemplateBody;

    // True while parsing a FOR TABLE template's member body, where INCLUDE members are rejected
    // (a table template cannot include another).
    private bool _inTableTemplateBody;

    private readonly List<NsqlSyntaxException> _errors = [];

    /// <summary>
    /// The syntax errors collected while parsing. The parser recovers at statement boundaries, so one
    /// parse reports every error in the document; the returned tree carries the statements that parsed.
    /// </summary>
    public IReadOnlyList<NsqlSyntaxException> Errors => _errors;

    public NsqlParser(string source)
    {
        _lexer = new NsqlLexer(source);
        _current = _lexer.Next();
    }

    /// <summary>
    /// Parses the whole document under the project grammar.
    /// </summary>
    public NsqlDocument Parse()
    {
        var statements = new List<NsqlStatement>();
        string? pendingDoc = null;

        while (_current.Kind != TokenKind.EndOfFile)
        {
            if (_current.Kind == TokenKind.DocComment)
            {
                // A doc-comment attaches to the declaration that follows it (last one wins).
                pendingDoc = _current.Text;
                Advance();
                continue;
            }

            try
            {
                statements.Add(ParseStatement(pendingDoc));
            }
            catch (NsqlSyntaxException error)
            {
                // Record and resync to the next statement boundary, so one parse reports every error.
                _errors.Add(error);
                Resync();
            }
            pendingDoc = null;
        }

        return new NsqlDocument(statements);
    }

    /// <summary>
    /// Skips to just past the next top-level <c>;</c> (the statement boundary). Strings and dollar-quoted
    /// bodies are single tokens, so a <c>;</c> inside them is never structural.
    /// </summary>
    private void Resync()
    {
        while (_current.Kind != TokenKind.EndOfFile)
        {
            if (Advance().Kind == TokenKind.Semicolon)
            {
                return;
            }
        }
    }

    // Dispatches on NsqlKeywords.StatementOpeners — a new opener is added there first, so the formatter
    // recognizes it as a statement boundary.
    private NsqlStatement ParseStatement(string? doc)
    {
        if (_current.IsKeyword("CREATE"))
        {
            return ParseCreate(doc);
        }
        if (_current.IsKeyword("DROP"))
        {
            return ParseDrop(doc);
        }
        if (_current.IsKeyword("GRANT"))
        {
            return ParseGrant(doc);
        }
        if (_current.IsKeyword("TEMPLATE"))
        {
            return ParseTemplate(doc);
        }
        if (_current.IsKeyword("APPLY"))
        {
            return ParseApplyTemplate(doc);
        }
        if (_current.IsKeyword("SCRIPT"))
        {
            return ParseScript(doc);
        }
        if (_current.IsKeyword("BACKEND") || _current.IsKeyword("PROVIDER"))
        {
            throw Error($"'{_current.Text.ToUpperInvariant()}' is a configuration statement; configuration lives in the project's configuration files, not among schema statements.");
        }
        if (_current.Kind == TokenKind.Identifier)
        {
            throw Error($"Unknown statement '{_current.Text}'.");
        }
        throw Error($"Unexpected '{_current.Text}'; expected a statement.");
    }

    // --- name nodes -------------------------------------------------------------

    private Identifier ExpectIdentifierNode(string what)
    {
        if (_current.Kind != TokenKind.Identifier)
        {
            throw Error($"Expected {what}.");
        }
        var token = Advance();
        return new Identifier(token.Text) { Position = token.Position };
    }

    private QualifiedName ParseQualifiedNameNode()
    {
        var first = ExpectIdentifierNode("a schema name");
        if (!Match(TokenKind.Dot))
        {
            // Inside a template body an unqualified name is stored as written; projection binds it.
            if (_inTemplateBody)
            {
                return new QualifiedName(null, first) { Position = first.Position };
            }
            throw Error("Expected '.'.");
        }
        var name = ExpectIdentifierNode("a table name");
        return new QualifiedName(first, name) { Position = first.Position };
    }

    // --- token cursor helpers -----------------------------------------------------

    private Token Advance()
    {
        var consumed = _current;
        _current = _lexer.Next();
        return consumed;
    }

    private Token Expect(TokenKind kind, string what)
    {
        if (_current.Kind != kind)
        {
            throw Error($"Expected {what}.");
        }
        return Advance();
    }

    private void ExpectKeyword(string keyword)
    {
        if (!_current.IsKeyword(keyword))
        {
            throw Error($"Expected '{keyword}'.");
        }
        Advance();
    }

    private string ExpectInteger() => Expect(TokenKind.Integer, "an integer").Text;

    private long ExpectIntegerValue() => long.Parse(ExpectInteger());

    private long ExpectSignedIntegerValue()
    {
        var negative = Match(TokenKind.Minus);
        var value = ExpectIntegerValue();
        return negative ? -value : value;
    }

    private bool Match(TokenKind kind)
    {
        if (_current.Kind != kind)
        {
            return false;
        }
        Advance();
        return true;
    }

    /// <summary>Consumes any doc-comments at the cursor, returning the last one's text (or null).</summary>
    private string? TakePendingDoc()
    {
        string? doc = null;
        while (_current.Kind == TokenKind.DocComment)
        {
            doc = _current.Text;
            Advance();
        }
        return doc;
    }

    private NsqlSyntaxException Error(string message) => new(message, _current.Position);

    // --- opaque-span capture --------------------------------------------------
    //
    // The lexer is context-free, so opaque expression text (view bodies, default expressions, routine definitions,
    // …) is recovered here by slicing the source between the offsets of the tokens we consume, rather than by the
    // lexer reading raw text on the parser's behalf. Strings and dollar-quoted bodies are single tokens, so a
    // terminator inside them is never structural; parentheses are balanced so a depth-zero terminator wins.

    /// <summary>
    /// Captures the verbatim source of a balanced parenthesised group at the cursor (which must be <c>(</c>),
    /// returning the inner text trimmed (possibly empty). Consumes through the matching <c>)</c>.
    /// </summary>
    private string CaptureParenthesized()
    {
        var open = Expect(TokenKind.LeftParen, "'(' to begin an expression");
        var innerStart = open.Position.Offset + 1; // just past '('
        var depth = 1;
        while (true)
        {
            if (_current.Kind == TokenKind.EndOfFile)
            {
                throw new NsqlSyntaxException("Unterminated expression", open.Position);
            }
            if (_current.Kind == TokenKind.LeftParen)
            {
                depth++;
            }
            else if (_current.Kind == TokenKind.RightParen && --depth == 0)
            {
                var inner = _lexer.Slice(innerStart, _current.Position.Offset).Trim();
                Advance(); // consume ')'
                return inner;
            }
            Advance();
        }
    }

    /// <summary>
    /// Captures the verbatim source from the cursor up to — but not consuming — a depth-zero terminator: one of the
    /// <paramref name="terminators"/> token kinds, or an identifier matching <paramref name="terminatorKeyword"/>.
    /// Returns it trimmed. Throws <c>Expected {what}</c> when the span is empty unless <paramref name="allowEmpty"/>.
    /// </summary>
    private string CaptureRawSpan(string what, ReadOnlySpan<TokenKind> terminators, string? terminatorKeyword = null, bool allowEmpty = false)
    {
        var startToken = _current;
        var depth = 0;
        while (true)
        {
            var kind = _current.Kind;
            if (kind == TokenKind.EndOfFile)
            {
                break;
            }
            if (kind == TokenKind.LeftParen)
            {
                depth++;
            }
            else if (kind == TokenKind.RightParen)
            {
                if (depth > 0)
                {
                    depth--;
                }
                else if (terminators.Contains(TokenKind.RightParen))
                {
                    break;
                }
            }
            else if (depth == 0)
            {
                if (terminators.Contains(kind))
                {
                    break;
                }
                if (terminatorKeyword is not null && _current.IsKeyword(terminatorKeyword))
                {
                    break;
                }
            }
            Advance();
        }

        var text = _lexer.Slice(startToken.Position.Offset, _current.Position.Offset).Trim();
        if (!allowEmpty && text.Length == 0)
        {
            throw new NsqlSyntaxException($"Expected {what}", startToken.Position);
        }
        return text;
    }
}
