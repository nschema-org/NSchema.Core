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
        Advance(); // TABLE
        var namePosition = _current.Position;
        var (schemaName, tableName) = ParseQualifiedName();
        var oldName = TryParseRenamedFrom();

        Expect(TokenKind.LeftParen, "'(' to begin the table body");
        var body = new TableBody();
        do
        {
            var itemDoc = TakePendingDoc();
            ParseTableItem(itemDoc, body);
        }
        while (Match(TokenKind.Comma));
        Expect(TokenKind.RightParen, "')' or ',' after a table member");
        Expect(TokenKind.Semicolon, "';'");

        var table = Table.Create(tableName, oldName, body.PrimaryKey, doc,
            body.Columns, body.ForeignKeys, body.UniqueConstraints, body.CheckConstraints, body.Indexes);
        schemas.AddTable(schemaName, table, namePosition);
    }

    private void ParseTableItem(string? doc, TableBody body)
    {
        if (_current.IsKeyword("CONSTRAINT"))
        {
            ParseConstraint(doc, body);
        }
        else if (_current.IsKeyword("UNIQUE"))
        {
            ParseIndex(doc, isUnique: true, body);
        }
        else if (_current.IsKeyword("INDEX"))
        {
            ParseIndex(doc, isUnique: false, body);
        }
        else if (_current.Kind == TokenKind.Identifier)
        {
            ParseColumn(doc, body);
        }
        else
        {
            throw Error("Expected a column or constraint definition.");
        }
    }

    private void ParseColumn(string? doc, TableBody body)
    {
        var name = ExpectIdentifier("a column name");
        var type = ParseType();

        var isNullable = true;
        if (_current.IsKeyword("NOT"))
        {
            Advance();
            ExpectKeyword("NULL");
            isNullable = false;
        }
        else if (_current.IsKeyword("NULL"))
        {
            Advance();
        }

        var isIdentity = false;
        IdentityOptions? identity = null;
        if (_current.IsKeyword("IDENTITY"))
        {
            Advance();
            isIdentity = true;
            identity = TryParseIdentityOptions();
        }

        string? defaultExpression = null;
        if (_current.IsKeyword("DEFAULT"))
        {
            Advance();
            defaultExpression = ReadRawExpression(parenthesised: false);
        }

        var oldName = TryParseRenamedFrom();

        body.Columns.Add(Column.Create(name, type, isNullable, isIdentity, defaultExpression, oldName, doc, identity));
    }

    private SqlType ParseType()
    {
        var text = ExpectIdentifier("a column type");
        if (_current.Kind == TokenKind.LeftParen)
        {
            Advance();
            var facets = ExpectInteger();
            if (Match(TokenKind.Comma))
            {
                facets += "," + ExpectInteger();
            }
            Expect(TokenKind.RightParen, "')'");
            text += $"({facets})";
        }
        return SqlType.Parse(text);
    }

    private IdentityOptions? TryParseIdentityOptions()
    {
        if (_current.Kind != TokenKind.LeftParen)
        {
            return null;
        }
        Advance();

        long? start = null, min = null, increment = null;
        do
        {
            var option = ExpectIdentifier("START, INCREMENT or MINVALUE");
            var value = ExpectIntegerValue();
            if (string.Equals(option, "START", StringComparison.OrdinalIgnoreCase))
            {
                start = value;
            }
            else if (string.Equals(option, "INCREMENT", StringComparison.OrdinalIgnoreCase))
            {
                increment = value;
            }
            else if (string.Equals(option, "MINVALUE", StringComparison.OrdinalIgnoreCase))
            {
                min = value;
            }
            else
            {
                throw Error($"Unknown identity option '{option}'; expected START, INCREMENT or MINVALUE.");
            }
        }
        while (Match(TokenKind.Comma));
        Expect(TokenKind.RightParen, "')'");

        return new IdentityOptions(start, min, increment);
    }

    private void ParseConstraint(string? doc, TableBody body)
    {
        Advance(); // CONSTRAINT
        var name = ExpectIdentifier("a constraint name");

        if (_current.IsKeyword("PRIMARY"))
        {
            Advance();
            ExpectKeyword("KEY");
            var columns = ParseColumnList();
            if (body.PrimaryKey is not null)
            {
                throw Error("A table may declare only one primary key.");
            }
            body.PrimaryKey = new PrimaryKey(name, columns, doc);
        }
        else if (_current.IsKeyword("FOREIGN"))
        {
            Advance();
            ExpectKeyword("KEY");
            var columns = ParseColumnList();
            ExpectKeyword("REFERENCES");
            var (refSchema, refTable) = ParseQualifiedName();
            var refColumns = ParseColumnList();
            var (onDelete, onUpdate) = ParseReferentialActions();
            body.ForeignKeys.Add(new ForeignKey(name, columns, refSchema, refTable, refColumns, onDelete, onUpdate, doc));
        }
        else if (_current.IsKeyword("UNIQUE"))
        {
            Advance();
            var columns = ParseColumnList();
            body.UniqueConstraints.Add(new UniqueConstraint(name, columns, doc));
        }
        else if (_current.IsKeyword("CHECK"))
        {
            Advance();
            var expression = ReadRawExpression(parenthesised: true);
            body.CheckConstraints.Add(new CheckConstraint(name, expression, doc));
        }
        else
        {
            throw Error($"Expected PRIMARY KEY, FOREIGN KEY, UNIQUE or CHECK, found '{_current.Text}'.");
        }
    }

    private (ReferentialAction OnDelete, ReferentialAction OnUpdate) ParseReferentialActions()
    {
        var onDelete = ReferentialAction.NoAction;
        var onUpdate = ReferentialAction.NoAction;
        while (_current.IsKeyword("ON"))
        {
            Advance();
            if (_current.IsKeyword("DELETE"))
            {
                Advance();
                onDelete = ParseReferentialAction();
            }
            else if (_current.IsKeyword("UPDATE"))
            {
                Advance();
                onUpdate = ParseReferentialAction();
            }
            else
            {
                throw Error($"Expected DELETE or UPDATE after ON, found '{_current.Text}'.");
            }
        }
        return (onDelete, onUpdate);
    }

    private ReferentialAction ParseReferentialAction()
    {
        if (_current.IsKeyword("NO"))
        {
            Advance();
            ExpectKeyword("ACTION");
            return ReferentialAction.NoAction;
        }
        if (_current.IsKeyword("CASCADE"))
        {
            Advance();
            return ReferentialAction.Cascade;
        }
        if (_current.IsKeyword("SET"))
        {
            Advance();
            if (_current.IsKeyword("NULL"))
            {
                Advance();
                return ReferentialAction.SetNull;
            }
            if (_current.IsKeyword("DEFAULT"))
            {
                Advance();
                return ReferentialAction.SetDefault;
            }
            throw Error($"Expected NULL or DEFAULT after SET, found '{_current.Text}'.");
        }
        throw Error($"Expected a referential action (NO ACTION, CASCADE, SET NULL, SET DEFAULT), found '{_current.Text}'.");
    }

    private void ParseIndex(string? doc, bool isUnique, TableBody body)
    {
        if (isUnique)
        {
            Advance(); // UNIQUE
        }
        ExpectKeyword("INDEX");
        var name = ExpectIdentifier("an index name");
        var columns = ParseColumnList();

        string? predicate = null;
        if (_current.IsKeyword("WHERE"))
        {
            Advance();
            predicate = ReadRawExpression(parenthesised: true);
        }

        body.Indexes.Add(new TableIndex(name, columns, isUnique, doc, predicate));
    }

    private void ParseGrant(SchemaAccumulator schemas)
    {
        Advance(); // GRANT

        if (_current.IsKeyword("USAGE"))
        {
            Advance();
            ExpectKeyword("ON");
            ExpectKeyword("SCHEMA");
            var schema = ExpectIdentifier("a schema name");
            ExpectKeyword("TO");
            var role = ExpectIdentifier("a role name");
            Expect(TokenKind.Semicolon, "';'");
            schemas.AddSchemaGrant(schema, role);
            return;
        }

        var privileges = ParseTablePrivileges();
        ExpectKeyword("ON");
        var position = _current.Position;
        var (schemaName, tableName) = ParseQualifiedName();
        ExpectKeyword("TO");
        var grantee = ExpectIdentifier("a role name");
        Expect(TokenKind.Semicolon, "';'");
        schemas.AddTableGrant(schemaName, tableName, new TableGrant(grantee, privileges), position);
    }

    private TablePrivilege ParseTablePrivileges()
    {
        var privileges = ParseTablePrivilege();
        while (Match(TokenKind.Comma))
        {
            privileges |= ParseTablePrivilege();
        }
        return privileges;
    }

    private TablePrivilege ParseTablePrivilege()
    {
        if (_current.IsKeyword("SELECT")) { Advance(); return TablePrivilege.Select; }
        if (_current.IsKeyword("INSERT")) { Advance(); return TablePrivilege.Insert; }
        if (_current.IsKeyword("UPDATE")) { Advance(); return TablePrivilege.Update; }
        if (_current.IsKeyword("DELETE")) { Advance(); return TablePrivilege.Delete; }
        throw Error($"Expected a privilege (SELECT, INSERT, UPDATE, DELETE), found '{_current.Text}'.");
    }

    private List<string> ParseColumnList()
    {
        Expect(TokenKind.LeftParen, "'('");
        var columns = new List<string> { ExpectIdentifier("a column name") };
        while (Match(TokenKind.Comma))
        {
            columns.Add(ExpectIdentifier("a column name"));
        }
        Expect(TokenKind.RightParen, "')'");
        return columns;
    }

    /// <summary>
    /// Captures an opaque SQL expression as raw text by rewinding the scanner to the current lookahead token and
    /// re-reading from there: a balanced <c>( … )</c> when <paramref name="parenthesised"/> (CHECK / WHERE), or an
    /// unparenthesised DEFAULT value otherwise. Re-syncs the lookahead afterwards.
    /// </summary>
    private string ReadRawExpression(bool parenthesised)
    {
        _lexer.ResetTo(_current.Position);
        var expression = parenthesised ? _lexer.ReadParenthesizedExpression() : _lexer.ReadDefaultExpression();
        _current = _lexer.Next();
        return expression;
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

    private string ExpectInteger() => Expect(TokenKind.Integer, "an integer").Text;

    private long ExpectIntegerValue() => long.Parse(ExpectInteger());

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

    private DslSyntaxException Error(string message) => new(message, _current.Position);

    /// <summary>Mutable scratch space for the members of one table as its body is parsed.</summary>
    private sealed class TableBody
    {
        public PrimaryKey? PrimaryKey { get; set; }
        public List<Column> Columns { get; } = [];
        public List<ForeignKey> ForeignKeys { get; } = [];
        public List<UniqueConstraint> UniqueConstraints { get; } = [];
        public List<CheckConstraint> CheckConstraints { get; } = [];
        public List<TableIndex> Indexes { get; } = [];
    }

    /// <summary>
    /// Accumulates parsed statements into a <see cref="DatabaseSchema"/>. Schema entries are vivified on demand so
    /// a <c>DROP TABLE app.x</c> can record the drop even when <c>app</c> was never explicitly declared.
    /// </summary>
    private sealed class SchemaAccumulator
    {
        private readonly List<Entry> _entries = [];
        private readonly Dictionary<string, Entry> _byName = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _droppedSchemas = [];
        private readonly List<PendingGrant> _tableGrants = [];

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

        public void AddTable(string schema, Table table, SourcePosition position)
        {
            var entry = GetOrAdd(schema);
            if (entry.Tables.Any(t => t.Name == table.Name))
            {
                throw new DslSyntaxException($"Table '{schema}.{table.Name}' is already declared.", position);
            }
            entry.Tables.Add(table);
        }

        public void AddSchemaGrant(string schema, string role)
        {
            var entry = GetOrAdd(schema);
            if (entry.Grants.All(g => g.Role != role))
            {
                entry.Grants.Add(new SchemaGrant(role));
            }
        }

        // Table grants are resolved at Build, so a grant may appear before or after the table it targets.
        public void AddTableGrant(string schema, string table, TableGrant grant, SourcePosition position)
            => _tableGrants.Add(new PendingGrant(schema, table, grant, position));

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
            ApplyTableGrants();
            var schemas = _entries
                .Select(e => SchemaDefinition.Create(e.Name, e.OldName, e.IsPartial, e.Comment, e.Tables, e.DroppedTables, e.Grants))
                .ToList();
            return DatabaseSchema.Create(schemas, _droppedSchemas);
        }

        private void ApplyTableGrants()
        {
            foreach (var pending in _tableGrants)
            {
                if (!_byName.TryGetValue(pending.Schema, out var entry))
                {
                    throw new DslSyntaxException($"GRANT references unknown table '{pending.Schema}.{pending.Table}'.", pending.Position);
                }

                var index = entry.Tables.FindIndex(t => t.Name == pending.Table);
                if (index < 0)
                {
                    throw new DslSyntaxException($"GRANT references unknown table '{pending.Schema}.{pending.Table}'.", pending.Position);
                }

                var table = entry.Tables[index];
                entry.Tables[index] = table with { Grants = [.. table.Grants, pending.Grant] };
            }
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
            public List<SchemaGrant> Grants { get; } = [];
        }

        private readonly record struct PendingGrant(string Schema, string Table, TableGrant Grant, SourcePosition Position);
    }
}
