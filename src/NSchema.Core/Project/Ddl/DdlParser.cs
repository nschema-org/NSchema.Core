using NSchema.Project.Ddl.Models;
using NSchema.Project.Ddl.Models.Config;
using NSchema.Project.Ddl.Models.Templates;
using NSchema.Project.Domain.Models.Scripts;

namespace NSchema.Project.Ddl;

/// <summary>
/// Recursive-descent parser for NSchema DDL.
/// </summary>
internal sealed partial class DdlParser
{
    private readonly DdlLexer _lexer;
    private Token _current;

    // Stores the name of the current schema while parsing a TEMPLATE body.
    // This is used to automatically qualify any identifiers missing a schema.
    private string? _templateSchemaContext;

    // True while parsing a FOR TABLE template's member body, where INCLUDE members are rejected (a table
    // template cannot include another).
    private bool _inTableTemplateBody;

    public DdlParser(string source)
    {
        _lexer = new DdlLexer(source);
        _current = _lexer.Next();
    }

    /// <summary>
    /// Parses the whole document.
    /// </summary>
    public DdlDocument Parse()
    {
        var document = new DocumentAccumulator();
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

            ParseStatement(document, pendingDoc);
            pendingDoc = null;
        }

        return document.Build();
    }

    private void ParseStatement(DocumentAccumulator document, string? doc)
    {
        if (_current.IsKeyword("CREATE"))
        {
            ParseCreate(document.Schemas, doc);
        }
        else if (_current.IsKeyword("DROP"))
        {
            ParseDrop(document.Schemas);
        }
        else if (_current.IsKeyword("GRANT"))
        {
            ParseGrant(document.Schemas);
        }
        else if (_current.IsKeyword("TEMPLATE"))
        {
            document.Templates.Add(ParseTemplate());
        }
        else if (_current.IsKeyword("APPLY"))
        {
            document.Applications.Add(ParseApplyTemplate());
        }
        else if (_current.IsKeyword("SCRIPT"))
        {
            ParseScript(document.Scripts);
        }
        else if (_current.IsKeyword("BACKEND") || _current.IsKeyword("PROVIDER"))
        {
            document.Config.Add(ParseConfigBlock());
        }
        else if (_current.Kind == TokenKind.Identifier)
        {
            throw Error($"Unknown statement '{_current.Text}'.");
        }
        else
        {
            throw Error($"Unexpected '{_current.Text}'; expected a statement.");
        }
    }

    /// <summary>
    /// Accumulates the results of top-level statements — the parse-wide context threaded through statement
    /// dispatch, so a new statement kind adds a field here rather than a parameter to every signature. Statements
    /// that write schema objects receive the narrower <see cref="SchemaAccumulator"/> (<see cref="Schemas"/>);
    /// a template body substitutes its own.
    /// </summary>
    private sealed class DocumentAccumulator
    {
        public SchemaAccumulator Schemas { get; } = new();
        public List<ConfigBlock> Config { get; } = [];
        public List<Script> Scripts { get; } = [];
        public List<TemplateDefinition> Templates { get; } = [];
        public List<TemplateApplication> Applications { get; } = [];

        public DdlDocument Build() => new(Schemas.Build(), Config, Scripts)
        {
            Templates = new TemplateSet(Templates, Applications, Schemas.Includes),
        };
    }

    // --- shared helpers -------------------------------------------------------

    private (string Schema, string Table) ParseQualifiedName()
    {
        var schema = ExpectIdentifier("a schema name");
        if (!Match(TokenKind.Dot))
        {
            // Inside a template body an unqualified name binds to the current schema context.
            if (_templateSchemaContext is { } templateSchema)
            {
                return (templateSchema, schema);
            }
            throw Error("Expected '.'.");
        }
        var table = ExpectIdentifier("a table name");
        return (schema, table);
    }

    // --- token cursor helpers -------------------------------------------------

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

    private string ExpectIdentifier(string what)
    {
        if (_current.Kind != TokenKind.Identifier)
        {
            throw Error($"Expected {what}.");
        }
        return Advance().Text;
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

    private DdlSyntaxException Error(string message) => new(message, _current.Position);


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
                throw new DdlSyntaxException("Unterminated expression", open.Position);
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
            throw new DdlSyntaxException($"Expected {what}", startToken.Position);
        }
        return text;
    }
}
