using NSchema.Configuration;
using NSchema.Schema.Model;

namespace NSchema.Schema.Serialization.Ddl;

/// <summary>
/// Recursive-descent parser for NSchema DDL
/// </summary>
internal sealed class DdlParser
{
    private readonly DdlLexer _lexer;
    private Token _current;

    public DdlParser(string source)
    {
        _lexer = new DdlLexer(source);
        _current = _lexer.Next();
    }

    /// <summary>
    /// Parses the whole document into a <see cref="DatabaseSchema"/>.
    /// </summary>
    public DatabaseSchema Parse() => ParseDocument().Schema;

    /// <summary>
    /// Parses the whole document into a <see cref="DdlDocument"/>.
    /// </summary>
    public DdlDocument ParseDocument()
    {
        var schemas = new SchemaAccumulator();
        var config = new List<ConfigBlock>();
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

            ParseStatement(schemas, config, pendingDoc);
            pendingDoc = null;
        }

        return new DdlDocument(schemas.Build(), config);
    }

    private void ParseStatement(SchemaAccumulator schemas, List<ConfigBlock> config, string? doc)
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
            // Any other top-level keyword introduces a configuration block (NSCHEMA / BACKEND / PROVIDER …).
            // The core captures it but never interprets it, so an unknown block keyword is captured rather than
            // rejected.
            config.Add(ParseConfigBlock());
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
        else if (_current.IsKeyword("VIEW"))
        {
            if (partial)
            {
                throw Error("PARTIAL applies to SCHEMA, not VIEW.");
            }
            ParseCreateView(schemas, doc);
        }
        else if (_current.IsKeyword("ENUM"))
        {
            if (partial)
            {
                throw Error("PARTIAL applies to SCHEMA, not ENUM.");
            }
            ParseCreateEnum(schemas, doc);
        }
        else if (_current.IsKeyword("SEQUENCE"))
        {
            if (partial)
            {
                throw Error("PARTIAL applies to SCHEMA, not SEQUENCE.");
            }
            ParseCreateSequence(schemas, doc);
        }
        else if (_current.IsKeyword("FUNCTION"))
        {
            if (partial)
            {
                throw Error("PARTIAL applies to SCHEMA, not FUNCTION.");
            }
            ParseCreateFunction(schemas, doc);
        }
        else if (_current.IsKeyword("PROCEDURE"))
        {
            if (partial)
            {
                throw Error("PARTIAL applies to SCHEMA, not PROCEDURE.");
            }
            ParseCreateProcedure(schemas, doc);
        }
        else
        {
            throw Error($"Expected SCHEMA, TABLE, VIEW, ENUM, SEQUENCE, FUNCTION or PROCEDURE after CREATE, found '{_current.Text}'.");
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

        var table = new Table(tableName, oldName, body.PrimaryKey, doc,
            body.Columns, body.ForeignKeys, body.UniqueConstraints, body.CheckConstraints, body.Indexes);
        schemas.AddTable(schemaName, table, namePosition);
    }

    private void ParseCreateView(SchemaAccumulator schemas, string? doc)
    {
        Advance(); // VIEW
        var namePosition = _current.Position;
        var (schemaName, viewName) = ParseQualifiedName();
        var oldName = TryParseRenamedFrom();
        ExpectKeyword("AS");

        // The body is captured verbatim; its FROM/JOIN targets become the view's dependencies.
        _lexer.ResetTo(_current.Position);
        var body = _lexer.ReadRawSpan("a view body", ";");
        _current = _lexer.Next();
        Expect(TokenKind.Semicolon, "';' to end the view definition");

        var dependsOn = ViewDependencyExtractor.Extract(body, schemaName);
        schemas.AddView(schemaName, new View(viewName, body, oldName, doc, dependsOn), namePosition);
    }

    private void ParseCreateFunction(SchemaAccumulator schemas, string? doc)
    {
        Advance(); // FUNCTION
        var namePosition = _current.Position;
        var (schemaName, functionName) = ParseQualifiedName();
        var oldName = TryParseRenamedFrom();
        var (arguments, definition) = ReadRoutineArgumentsAndDefinition("a function definition");
        Expect(TokenKind.Semicolon, "';' to end the function definition");

        schemas.AddFunction(schemaName, new Function(functionName, arguments, definition, oldName, doc), namePosition);
    }

    private void ParseCreateProcedure(SchemaAccumulator schemas, string? doc)
    {
        Advance(); // PROCEDURE
        var namePosition = _current.Position;
        var (schemaName, procedureName) = ParseQualifiedName();
        var oldName = TryParseRenamedFrom();
        var (arguments, definition) = ReadRoutineArgumentsAndDefinition("a procedure definition");
        Expect(TokenKind.Semicolon, "';' to end the procedure definition");

        schemas.AddProcedure(schemaName, new Procedure(procedureName, arguments, definition, oldName, doc), namePosition);
    }

    /// <summary>
    /// Captures a routine's argument list (the verbatim text inside the parentheses) and its definition (the
    /// verbatim, dollar-quote-aware text up to the top-level <c>;</c>).
    /// </summary>
    private (string Arguments, string Definition) ReadRoutineArgumentsAndDefinition(string what)
    {
        _lexer.ResetTo(_current.Position);
        var arguments = _lexer.ReadParenthesizedExpression();
        var definition = _lexer.ReadRawSpan(what, ";");
        _current = _lexer.Next();
        return (arguments, definition);
    }

    private void ParseCreateEnum(SchemaAccumulator schemas, string? doc)
    {
        Advance(); // ENUM
        var namePosition = _current.Position;
        var (schemaName, enumName) = ParseQualifiedName();
        var oldName = TryParseRenamedFrom();

        Expect(TokenKind.LeftParen, "'(' to begin the enum values");
        var values = new List<string>();
        if (_current.Kind != TokenKind.RightParen)
        {
            do
            {
                var valuePosition = _current.Position;
                var value = Expect(TokenKind.String, "an enum value (a quoted string)").Text;
                if (values.Contains(value, StringComparer.Ordinal))
                {
                    throw new DdlSyntaxException($"Enum value '{value}' is declared more than once.", valuePosition);
                }
                values.Add(value);
            }
            while (Match(TokenKind.Comma));
        }
        Expect(TokenKind.RightParen, "')' or ',' after an enum value");
        Expect(TokenKind.Semicolon, "';'");

        schemas.AddEnum(schemaName, new EnumType(enumName, values, oldName, doc), namePosition);
    }

    private void ParseCreateSequence(SchemaAccumulator schemas, string? doc)
    {
        Advance(); // SEQUENCE
        var namePosition = _current.Position;
        var (schemaName, sequenceName) = ParseQualifiedName();
        var oldName = TryParseRenamedFrom();
        var options = TryParseSequenceOptions();
        Expect(TokenKind.Semicolon, "';'");

        schemas.AddSequence(schemaName, new Sequence(sequenceName, options, oldName, doc), namePosition);
    }

    private SequenceOptions? TryParseSequenceOptions()
    {
        if (_current.Kind != TokenKind.LeftParen)
        {
            return null;
        }
        Advance();

        SqlType? dataType = null;
        long? start = null, increment = null, min = null, max = null, cache = null;
        var cycle = false;
        do
        {
            var optionPosition = _current.Position;
            var option = ExpectIdentifier("a sequence option");

            void RejectDuplicate(bool alreadySet)
            {
                if (alreadySet)
                {
                    throw new DdlSyntaxException($"Sequence option '{option.ToUpperInvariant()}' is specified more than once.", optionPosition);
                }
            }

            if (string.Equals(option, "AS", StringComparison.OrdinalIgnoreCase))
            {
                RejectDuplicate(dataType is not null);
                dataType = SqlType.Parse(ExpectIdentifier("a type name"));
            }
            else if (string.Equals(option, "START", StringComparison.OrdinalIgnoreCase))
            {
                RejectDuplicate(start is not null);
                start = ExpectSignedIntegerValue();
            }
            else if (string.Equals(option, "INCREMENT", StringComparison.OrdinalIgnoreCase))
            {
                RejectDuplicate(increment is not null);
                increment = ExpectSignedIntegerValue();
            }
            else if (string.Equals(option, "MINVALUE", StringComparison.OrdinalIgnoreCase))
            {
                RejectDuplicate(min is not null);
                min = ExpectSignedIntegerValue();
            }
            else if (string.Equals(option, "MAXVALUE", StringComparison.OrdinalIgnoreCase))
            {
                RejectDuplicate(max is not null);
                max = ExpectSignedIntegerValue();
            }
            else if (string.Equals(option, "CACHE", StringComparison.OrdinalIgnoreCase))
            {
                RejectDuplicate(cache is not null);
                cache = ExpectSignedIntegerValue();
            }
            else if (string.Equals(option, "CYCLE", StringComparison.OrdinalIgnoreCase))
            {
                RejectDuplicate(cycle);
                cycle = true;
            }
            else
            {
                throw new DdlSyntaxException(
                    $"Unknown sequence option '{option}'; expected AS, START, INCREMENT, MINVALUE, MAXVALUE, CACHE or CYCLE.", optionPosition);
            }
        }
        while (Match(TokenKind.Comma));
        Expect(TokenKind.RightParen, "')'");

        return new SequenceOptions(dataType, start, increment, min, max, cache, cycle);
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

        body.Columns.Add(new Column(name, type, isNullable, isIdentity, defaultExpression, oldName, doc, identity));
    }

    private SqlType ParseType()
    {
        var text = ExpectIdentifier("a column type");
        if (Match(TokenKind.Dot))
        {
            // A schema-qualified user-defined type, e.g. an enum referenced as `app.status`.
            text += "." + ExpectIdentifier("a schema-qualified type name");
        }
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
    /// unparenthesised DEFAULT value otherwise (terminated by the enclosing column list's <c>,</c> / <c>)</c> or a
    /// <c>RENAMED</c> clause). Re-syncs the lookahead afterwards.
    /// </summary>
    private string ReadRawExpression(bool parenthesised)
    {
        _lexer.ResetTo(_current.Position);
        var expression = parenthesised
            ? _lexer.ReadParenthesizedExpression()
            : _lexer.ReadRawSpan("a default expression", ",)", "RENAMED");
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
        else if (_current.IsKeyword("VIEW"))
        {
            Advance();
            var (schema, view) = ParseQualifiedName();
            Expect(TokenKind.Semicolon, "';'");
            schemas.DropView(schema, view);
        }
        else if (_current.IsKeyword("ENUM"))
        {
            Advance();
            var (schema, enumName) = ParseQualifiedName();
            Expect(TokenKind.Semicolon, "';'");
            schemas.DropEnum(schema, enumName);
        }
        else if (_current.IsKeyword("SEQUENCE"))
        {
            Advance();
            var (schema, sequence) = ParseQualifiedName();
            Expect(TokenKind.Semicolon, "';'");
            schemas.DropSequence(schema, sequence);
        }
        else if (_current.IsKeyword("FUNCTION"))
        {
            Advance();
            var (schema, function) = ParseQualifiedName();
            Expect(TokenKind.Semicolon, "';'");
            schemas.DropFunction(schema, function);
        }
        else if (_current.IsKeyword("PROCEDURE"))
        {
            Advance();
            var (schema, procedure) = ParseQualifiedName();
            Expect(TokenKind.Semicolon, "';'");
            schemas.DropProcedure(schema, procedure);
        }
        else
        {
            throw Error($"Expected SCHEMA, TABLE, VIEW, ENUM, SEQUENCE, FUNCTION or PROCEDURE after DROP, found '{_current.Text}'.");
        }
    }

    /// <summary>
    /// Parses a configuration block: <c>keyword [label] ( key = value , … ) ;</c>. The keyword and optional label
    /// are bare identifiers; attributes are a flat, comma-separated list.
    /// </summary>
    private ConfigBlock ParseConfigBlock()
    {
        var type = ExpectIdentifier("a configuration block keyword").ToLowerInvariant();

        // An optional bare-identifier label, e.g. the 'postgres' in `PROVIDER postgres ( … )`. None for `NSCHEMA`.
        string? label = null;
        if (_current.Kind == TokenKind.Identifier)
        {
            label = Advance().Text;
        }

        Expect(TokenKind.LeftParen, "'(' to begin the configuration attributes");
        var attributes = new Dictionary<string, ConfigValue>(StringComparer.OrdinalIgnoreCase);
        if (_current.Kind != TokenKind.RightParen)
        {
            do
            {
                var keyPosition = _current.Position;
                var key = ParseConfigKey();
                Expect(TokenKind.Equals, "'=' after a configuration attribute name");
                var value = ParseConfigValue();
                if (!attributes.TryAdd(key, value))
                {
                    throw new DdlSyntaxException($"Configuration attribute '{key}' is specified more than once.", keyPosition);
                }
            }
            while (Match(TokenKind.Comma));
        }
        Expect(TokenKind.RightParen, "')' or ',' after a configuration attribute");
        Expect(TokenKind.Semicolon, "';'");

        return new ConfigBlock(type, label, attributes);
    }

    /// <summary>
    /// Parses a (possibly dotted) configuration attribute name, e.g. <c>path</c> or <c>pool.max</c>.
    /// </summary>
    private string ParseConfigKey()
    {
        var key = ExpectIdentifier("a configuration attribute name");
        while (Match(TokenKind.Dot))
        {
            key += "." + ExpectIdentifier("a configuration attribute name segment");
        }
        return key;
    }

    /// <summary>Parses a configuration scalar: string, (signed) integer, <c>true</c>/<c>false</c>, or bare identifier.</summary>
    private ConfigValue ParseConfigValue()
    {
        switch (_current.Kind)
        {
            case TokenKind.String:
                return ConfigValue.OfString(Advance().Text);
            case TokenKind.Integer:
            case TokenKind.Minus:
                return ConfigValue.OfInteger(ExpectSignedIntegerValue());
            case TokenKind.Identifier:
                var text = Advance().Text;
                if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase)) { return ConfigValue.OfBoolean(true); }
                if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase)) { return ConfigValue.OfBoolean(false); }
                return ConfigValue.OfIdentifier(text);
            default:
                throw Error("Expected a configuration value (a string, integer, true, false, or identifier).");
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
                throw new DdlSyntaxException($"Schema '{name}' is already declared.", position);
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
                throw new DdlSyntaxException($"Table '{schema}.{table.Name}' is already declared.", position);
            }
            entry.Tables.Add(table);
        }

        public void AddView(string schema, View view, SourcePosition position)
        {
            var entry = GetOrAdd(schema);
            if (entry.Views.Any(v => v.Name == view.Name))
            {
                throw new DdlSyntaxException($"View '{schema}.{view.Name}' is already declared.", position);
            }
            entry.Views.Add(view);
        }

        public void AddEnum(string schema, EnumType enumType, SourcePosition position)
        {
            var entry = GetOrAdd(schema);
            if (entry.Enums.Any(e => e.Name == enumType.Name))
            {
                throw new DdlSyntaxException($"Enum '{schema}.{enumType.Name}' is already declared.", position);
            }
            entry.Enums.Add(enumType);
        }

        public void AddSequence(string schema, Sequence sequence, SourcePosition position)
        {
            var entry = GetOrAdd(schema);
            if (entry.Sequences.Any(s => s.Name == sequence.Name))
            {
                throw new DdlSyntaxException($"Sequence '{schema}.{sequence.Name}' is already declared.", position);
            }
            entry.Sequences.Add(sequence);
        }

        // Functions and procedures share one name space, as they do in the database: a function and a procedure
        // with the same name cannot coexist in a schema.
        public void AddFunction(string schema, Function function, SourcePosition position)
        {
            var entry = GetOrAdd(schema);
            if (entry.Functions.Any(f => f.Name == function.Name))
            {
                throw new DdlSyntaxException($"Function '{schema}.{function.Name}' is already declared.", position);
            }
            if (entry.Procedures.Any(p => p.Name == function.Name))
            {
                throw new DdlSyntaxException(
                    $"Function '{schema}.{function.Name}' conflicts with a procedure of the same name; functions and procedures share one name space.", position);
            }
            entry.Functions.Add(function);
        }

        public void AddProcedure(string schema, Procedure procedure, SourcePosition position)
        {
            var entry = GetOrAdd(schema);
            if (entry.Procedures.Any(p => p.Name == procedure.Name))
            {
                throw new DdlSyntaxException($"Procedure '{schema}.{procedure.Name}' is already declared.", position);
            }
            if (entry.Functions.Any(f => f.Name == procedure.Name))
            {
                throw new DdlSyntaxException(
                    $"Procedure '{schema}.{procedure.Name}' conflicts with a function of the same name; functions and procedures share one name space.", position);
            }
            entry.Procedures.Add(procedure);
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

        public void DropView(string schema, string view)
        {
            var entry = GetOrAdd(schema);
            if (!entry.DroppedViews.Contains(view, StringComparer.OrdinalIgnoreCase))
            {
                entry.DroppedViews.Add(view);
            }
        }

        public void DropEnum(string schema, string enumName)
        {
            var entry = GetOrAdd(schema);
            if (!entry.DroppedEnums.Contains(enumName, StringComparer.OrdinalIgnoreCase))
            {
                entry.DroppedEnums.Add(enumName);
            }
        }

        public void DropSequence(string schema, string sequence)
        {
            var entry = GetOrAdd(schema);
            if (!entry.DroppedSequences.Contains(sequence, StringComparer.OrdinalIgnoreCase))
            {
                entry.DroppedSequences.Add(sequence);
            }
        }

        public void DropFunction(string schema, string function)
        {
            var entry = GetOrAdd(schema);
            if (!entry.DroppedFunctions.Contains(function, StringComparer.OrdinalIgnoreCase))
            {
                entry.DroppedFunctions.Add(function);
            }
        }

        public void DropProcedure(string schema, string procedure)
        {
            var entry = GetOrAdd(schema);
            if (!entry.DroppedProcedures.Contains(procedure, StringComparer.OrdinalIgnoreCase))
            {
                entry.DroppedProcedures.Add(procedure);
            }
        }

        public DatabaseSchema Build()
        {
            ApplyTableGrants();
            var schemas = _entries
                .Select(e => new SchemaDefinition(e.Name, e.OldName, e.IsPartial, e.Comment, e.Tables, e.DroppedTables, e.Grants, e.Views, e.DroppedViews,
                    e.Enums, e.DroppedEnums, e.Sequences, e.DroppedSequences,
                    e.Functions, e.DroppedFunctions, e.Procedures, e.DroppedProcedures))
                .ToList();
            return new DatabaseSchema(schemas, _droppedSchemas);
        }

        private void ApplyTableGrants()
        {
            foreach (var pending in _tableGrants)
            {
                if (!_byName.TryGetValue(pending.Schema, out var entry))
                {
                    throw new DdlSyntaxException($"GRANT references unknown table '{pending.Schema}.{pending.Table}'.", pending.Position);
                }

                var index = entry.Tables.FindIndex(t => t.Name == pending.Table);
                if (index < 0)
                {
                    throw new DdlSyntaxException($"GRANT references unknown table '{pending.Schema}.{pending.Table}'.", pending.Position);
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
            public List<View> Views { get; } = [];
            public List<string> DroppedViews { get; } = [];
            public List<EnumType> Enums { get; } = [];
            public List<string> DroppedEnums { get; } = [];
            public List<Sequence> Sequences { get; } = [];
            public List<string> DroppedSequences { get; } = [];
            public List<Function> Functions { get; } = [];
            public List<string> DroppedFunctions { get; } = [];
            public List<Procedure> Procedures { get; } = [];
            public List<string> DroppedProcedures { get; } = [];
        }

        private readonly record struct PendingGrant(string Schema, string Table, TableGrant Grant, SourcePosition Position);
    }
}
