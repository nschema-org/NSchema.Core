using NSchema.Schema.Model;

namespace NSchema.Schema.Serialization.Ddl;

/// <summary>
/// Recursive-descent parser for NSchema DDL (see <c>docs/dsl-grammar.md</c>). Turns a source document into a
/// <see cref="DatabaseSchema"/>. This is the schema consumer of the token stream: it understands
/// <c>CREATE</c>/<c>DROP</c>/<c>GRANT</c> statements and <em>skips</em> top-level configuration blocks (which are
/// reserved for a future front-end config loader, never the core).
/// </summary>
internal sealed class DslParser
{
    private readonly DslLexer _lexer;
    private Token _current;

    public DslParser(string source)
    {
        _lexer = new DslLexer(source);
        _current = _lexer.Next();
    }

    /// <summary>
    /// Parses the whole document into a <see cref="DatabaseSchema"/>.
    /// </summary>
    public DatabaseSchema Parse()
    {
        var schemas = new SchemaAccumulator();
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

            ParseStatement(schemas, pendingDoc);
            pendingDoc = null;
        }

        return schemas.Build();
    }

    private void ParseStatement(SchemaAccumulator schemas, string? doc)
    {
        if (_current.IsKeyword("CREATE"))
        {
            ParseCreate(schemas, doc);
        }
        else if (_current.IsKeyword("DROP"))
        {
            ParseDrop(schemas);
        }
        else if (_current.IsKeyword("GRANT"))
        {
            ParseGrant(schemas);
        }
        else if (_current.Kind == TokenKind.Identifier)
        {
            // An unrecognised top-level block is a (reserved) configuration block — accept and skip it, so the
            // grammar stays forward-compatible and the core never has to understand config.
            SkipConfigBlock();
        }
        else
        {
            throw Error($"Unexpected '{_current.Text}'; expected a statement.");
        }
    }

    private void ParseCreate(SchemaAccumulator schemas, string? doc)
    {
        Advance(); // CREATE

        var partial = false;
        if (_current.IsKeyword("PARTIAL"))
        {
            Advance();
            partial = true;
        }

        if (_current.IsKeyword("SCHEMA"))
        {
            ParseCreateSchema(schemas, doc, partial);
        }
        else if (_current.IsKeyword("TABLE"))
        {
            if (partial)
            {
                throw Error("PARTIAL applies to SCHEMA, not TABLE.");
            }
            ParseCreateTable(schemas, doc);
        }
        else
        {
            throw Error($"Expected SCHEMA or TABLE after CREATE, found '{_current.Text}'.");
        }
    }

    private void ParseCreateSchema(SchemaAccumulator schemas, string? doc, bool partial)
    {
        Advance(); // SCHEMA
        var namePosition = _current.Position;
        var name = ExpectIdentifier("a schema name");
        var oldName = TryParseRenamedFrom();
        Expect(TokenKind.Semicolon, "';'");
        schemas.DeclareSchema(name, oldName, partial, doc, namePosition);
    }

    private void ParseCreateTable(SchemaAccumulator schemas, string? doc)
    {
        // TODO(parser): table body (columns, constraints, inline indexes). Next increment.
        throw Error("CREATE TABLE is not yet supported by the parser.");
    }

    private void ParseGrant(SchemaAccumulator schemas)
    {
        // TODO(parser): GRANT … ON … / GRANT USAGE ON SCHEMA …. Next increment (needs tables to attach to).
        throw Error("GRANT is not yet supported by the parser.");
    }

    private void ParseDrop(SchemaAccumulator schemas)
    {
        Advance(); // DROP

        if (_current.IsKeyword("SCHEMA"))
        {
            Advance();
            var name = ExpectIdentifier("a schema name");
            Expect(TokenKind.Semicolon, "';'");
            schemas.DropSchema(name);
        }
        else if (_current.IsKeyword("TABLE"))
        {
            Advance();
            var (schema, table) = ParseQualifiedName();
            Expect(TokenKind.Semicolon, "';'");
            schemas.DropTable(schema, table);
        }
        else
        {
            throw Error($"Expected SCHEMA or TABLE after DROP, found '{_current.Text}'.");
        }
    }

    /// <summary>Skips a reserved configuration block: <c>ident [string] {{ … }}</c>, balancing nested braces.</summary>
    private void SkipConfigBlock()
    {
        Advance(); // the block-type identifier (e.g. nschema, backend, provider)
        if (_current.Kind == TokenKind.String)
        {
            Advance(); // optional label, e.g. backend "file"
        }
        Expect(TokenKind.LeftBrace, "'{' to begin a configuration block");

        var depth = 1;
        while (depth > 0)
        {
            switch (_current.Kind)
            {
                case TokenKind.EndOfFile:
                    throw Error("Unterminated configuration block.");
                case TokenKind.LeftBrace:
                    depth++;
                    break;
                case TokenKind.RightBrace:
                    depth--;
                    break;
            }
            Advance();
        }
    }

    private (string Schema, string Table) ParseQualifiedName()
    {
        var schema = ExpectIdentifier("a schema name");
        Expect(TokenKind.Dot, "'.'");
        var table = ExpectIdentifier("a table name");
        return (schema, table);
    }

    private string? TryParseRenamedFrom()
    {
        if (!_current.IsKeyword("RENAMED"))
        {
            return null;
        }
        Advance(); // RENAMED
        ExpectKeyword("FROM");
        return ExpectIdentifier("a previous name");
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

    private DslSyntaxException Error(string message) => new(message, _current.Position);

    /// <summary>
    /// Accumulates parsed statements into a <see cref="DatabaseSchema"/>. Schema entries are vivified on demand so
    /// a <c>DROP TABLE app.x</c> can record the drop even when <c>app</c> was never explicitly declared.
    /// </summary>
    private sealed class SchemaAccumulator
    {
        private readonly List<Entry> _entries = [];
        private readonly Dictionary<string, Entry> _byName = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _droppedSchemas = [];

        public void DeclareSchema(string name, string? oldName, bool isPartial, string? comment, SourcePosition position)
        {
            var entry = GetOrAdd(name);
            if (entry.Declared)
            {
                throw new DslSyntaxException($"Schema '{name}' is already declared.", position);
            }
            entry.Declared = true;
            entry.OldName = oldName;
            entry.IsPartial = isPartial;
            entry.Comment = comment;
        }

        public void DropSchema(string name)
        {
            if (!_droppedSchemas.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                _droppedSchemas.Add(name);
            }
        }

        public void DropTable(string schema, string table)
        {
            var entry = GetOrAdd(schema);
            if (!entry.DroppedTables.Contains(table, StringComparer.OrdinalIgnoreCase))
            {
                entry.DroppedTables.Add(table);
            }
        }

        public DatabaseSchema Build()
        {
            var schemas = _entries
                .Select(e => SchemaDefinition.Create(e.Name, e.OldName, e.IsPartial, e.Comment, e.Tables, e.DroppedTables))
                .ToList();
            return DatabaseSchema.Create(schemas, _droppedSchemas);
        }

        private Entry GetOrAdd(string name)
        {
            if (_byName.TryGetValue(name, out var existing))
            {
                return existing;
            }
            var entry = new Entry(name);
            _entries.Add(entry);
            _byName[name] = entry;
            return entry;
        }

        private sealed class Entry(string name)
        {
            public string Name { get; } = name;
            public bool Declared { get; set; }
            public string? OldName { get; set; }
            public bool IsPartial { get; set; }
            public string? Comment { get; set; }
            public List<Table> Tables { get; } = [];
            public List<string> DroppedTables { get; } = [];
        }
    }
}
