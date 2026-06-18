using NSchema.Configuration;
using NSchema.Schema.Ddl.Model;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.Constraints;
using NSchema.Schema.Model.Enums;
using NSchema.Schema.Model.Extensions;
using NSchema.Schema.Model.Indexes;
using NSchema.Schema.Model.Routines;
using NSchema.Schema.Model.Schemas;
using NSchema.Schema.Model.Scripts;
using NSchema.Schema.Model.Sequences;
using NSchema.Schema.Model.Tables;
using NSchema.Schema.Model.Triggers;
using NSchema.Schema.Model.Views;

namespace NSchema.Schema.Ddl;

/// <summary>
/// Recursive-descent parser for NSchema DDL.
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
    /// Parses the whole document into a <see cref="DdlDocument"/>.
    /// </summary>
    public DdlDocument Parse()
    {
        var schemas = new SchemaAccumulator();
        var config = new List<ConfigBlock>();
        var scripts = new List<Script>();
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

            ParseStatement(schemas, config, scripts, pendingDoc);
            pendingDoc = null;
        }

        return new DdlDocument(schemas.Build(), config, scripts);
    }

    private void ParseStatement(SchemaAccumulator schemas, List<ConfigBlock> config, List<Script> scripts, string? doc)
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
        else if (_current.IsKeyword("PRE"))
        {
            scripts.Add(ParseDeploymentScript(ScriptType.PreDeployment));
        }
        else if (_current.IsKeyword("POST"))
        {
            scripts.Add(ParseDeploymentScript(ScriptType.PostDeployment));
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

    /// <summary>
    /// Parses a deployment-script statement: <c>PRE|POST DEPLOYMENT '&lt;name&gt;' [( option = value, … )] AS $$ … $$;</c>.
    /// The body is opaque SQL (dollar-quoted, so it may contain its own <c>;</c>) run as-is around the migration —
    /// captured verbatim like a <c>CREATE VIEW … AS</c> body.
    /// </summary>
    private Script ParseDeploymentScript(ScriptType type)
    {
        Advance(); // PRE | POST
        ExpectKeyword("DEPLOYMENT");
        var name = Expect(TokenKind.String, "a quoted script name").Text;
        var runOutsideTransaction = ParseDeploymentScriptOptions();

        // The body is opaque (dollar-quoted) SQL, so — like a view body — it is captured by a raw lexer read rather
        // than tokenised. The 'AS' keyword is the anchor: we verify it without advancing onto the un-tokenisable '$',
        // leaving the lexer positioned right after it to read the body.
        if (!_current.IsKeyword("AS"))
        {
            throw Error("Expected 'AS' before the deployment script body.");
        }
        var body = _lexer.ReadDollarQuotedBody("a deployment script body").Trim();
        _current = _lexer.Next();
        Expect(TokenKind.Semicolon, "';' to end the deployment script");

        return new Script(name, body, type) { RunOutsideTransaction = runOutsideTransaction };
    }

    /// <summary>
    /// Parses the optional <c>( run_outside_transaction = true )</c> clause, returning the flag (default false).
    /// </summary>
    private bool ParseDeploymentScriptOptions()
    {
        if (_current.Kind != TokenKind.LeftParen)
        {
            return false;
        }

        Advance(); // (
        var runOutsideTransaction = false;
        if (_current.Kind != TokenKind.RightParen)
        {
            do
            {
                var keyPosition = _current.Position;
                var key = ExpectIdentifier("a deployment script option name");
                Expect(TokenKind.Equals, "'=' after an option name");
                var value = ParseConfigValue();
                switch (key.ToLowerInvariant())
                {
                    case "run_outside_transaction":
                        runOutsideTransaction = value.AsBoolean();
                        break;
                    default:
                        throw new DdlSyntaxException($"Unknown deployment script option '{key}'. Expected 'run_outside_transaction'.", keyPosition);
                }
            }
            while (Match(TokenKind.Comma));
        }
        Expect(TokenKind.RightParen, "')' or ',' after a deployment script option");
        return runOutsideTransaction;
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
            Advance(); // VIEW
            ParseCreateView(schemas, doc, materialized: false);
        }
        else if (_current.IsKeyword("MATERIALIZED"))
        {
            if (partial)
            {
                throw Error("PARTIAL applies to SCHEMA, not MATERIALIZED VIEW.");
            }
            Advance(); // MATERIALIZED
            ExpectKeyword("VIEW");
            ParseCreateView(schemas, doc, materialized: true);
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
            ParseCreateRoutine(schemas, doc, RoutineKind.Function);
        }
        else if (_current.IsKeyword("PROCEDURE"))
        {
            if (partial)
            {
                throw Error("PARTIAL applies to SCHEMA, not PROCEDURE.");
            }
            ParseCreateRoutine(schemas, doc, RoutineKind.Procedure);
        }
        else if (_current.IsKeyword("EXTENSION"))
        {
            if (partial)
            {
                throw Error("PARTIAL applies to SCHEMA, not EXTENSION.");
            }
            ParseCreateExtension(schemas, doc);
        }
        else if (_current.IsKeyword("TRIGGER"))
        {
            if (partial)
            {
                throw Error("PARTIAL applies to SCHEMA, not TRIGGER.");
            }
            ParseCreateTrigger(schemas, doc);
        }
        else if (_current.IsKeyword("INDEX"))
        {
            if (partial)
            {
                throw Error("PARTIAL applies to SCHEMA, not INDEX.");
            }
            ParseCreateIndex(schemas, doc, unique: false);
        }
        else if (_current.IsKeyword("UNIQUE"))
        {
            if (partial)
            {
                throw Error("PARTIAL applies to SCHEMA, not UNIQUE INDEX.");
            }
            Advance(); // UNIQUE
            ParseCreateIndex(schemas, doc, unique: true);
        }
        else
        {
            throw Error($"Expected SCHEMA, TABLE, VIEW, MATERIALIZED VIEW, ENUM, SEQUENCE, FUNCTION, PROCEDURE, EXTENSION, TRIGGER or INDEX after CREATE, found '{_current.Text}'.");
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

    // The "VIEW" keyword has already been consumed by the dispatcher (preceded by "MATERIALIZED" when
    // <paramref name="materialized"/> is true).
    private void ParseCreateView(SchemaAccumulator schemas, string? doc, bool materialized)
    {
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
        schemas.AddView(schemaName, new View(viewName, body, oldName, doc, dependsOn, materialized), namePosition);
    }

    /// <summary>
    /// Parses a standalone index: <c>CREATE [UNIQUE] INDEX name ON s.relation (cols) [WHERE (expr)]</c>. The
    /// "UNIQUE" keyword (if present) has already been consumed by the dispatcher. Standalone indexes attach at
    /// build time to the named relation — a table (equivalent to declaring it inline in the table body) or a
    /// materialized view (a plain view cannot carry one).
    /// </summary>
    private void ParseCreateIndex(SchemaAccumulator schemas, string? doc, bool unique)
    {
        ExpectKeyword("INDEX");
        var namePosition = _current.Position;
        var name = ExpectIdentifier("an index name");
        ExpectKeyword("ON");
        var (schemaName, relationName) = ParseQualifiedName();
        var columns = ParseColumnList();

        string? predicate = null;
        if (_current.IsKeyword("WHERE"))
        {
            Advance();
            predicate = ReadRawExpression(parenthesised: true);
        }
        Expect(TokenKind.Semicolon, "';'");

        schemas.AddIndex(schemaName, relationName, new TableIndex(name, columns, unique, doc, predicate), namePosition);
    }

    private void ParseCreateRoutine(SchemaAccumulator schemas, string? doc, RoutineKind kind)
    {
        Advance(); // FUNCTION | PROCEDURE
        var what = kind == RoutineKind.Procedure ? "a procedure definition" : "a function definition";
        var namePosition = _current.Position;
        var (schemaName, routineName) = ParseQualifiedName();
        var oldName = TryParseRenamedFrom();
        var (arguments, definition) = ReadRoutineArgumentsAndDefinition(what);
        Expect(TokenKind.Semicolon, $"';' to end {what}");

        schemas.AddRoutine(schemaName, new Routine(routineName, kind, arguments, definition, oldName, doc), namePosition);
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

    private void ParseCreateExtension(SchemaAccumulator schemas, string? doc)
    {
        Advance(); // EXTENSION
        var namePosition = _current.Position;
        var name = ParseExtensionName();
        string? version = null;
        if (_current.IsKeyword("VERSION"))
        {
            Advance();
            version = Expect(TokenKind.String, "a version string after VERSION").Text;
        }
        Expect(TokenKind.Semicolon, "';'");

        schemas.AddExtension(new Extension(name, version, doc), namePosition);
    }

    /// <summary>
    /// Reads an extension name, which may be a bare identifier (<c>citext</c>) or a quoted string
    /// (<c>'uuid-ossp'</c>) — extension names commonly contain characters, such as a hyphen, that a bare
    /// identifier cannot.
    /// </summary>
    private string ParseExtensionName() =>
        _current.Kind == TokenKind.String ? Advance().Text : ExpectIdentifier("an extension name");

    /// <summary>
    /// Parses a standalone trigger: <c>CREATE TRIGGER name {BEFORE|AFTER|INSTEAD OF} event {OR event} ON s.t
    /// [FOR EACH {ROW|STATEMENT}] [WHEN (expr)] EXECUTE {FUNCTION|PROCEDURE} f(args)</c>. Like a <c>GRANT</c>, it
    /// names its table via <c>ON</c> and is attached to that table at build time.
    /// </summary>
    private void ParseCreateTrigger(SchemaAccumulator schemas, string? doc)
    {
        Advance(); // TRIGGER
        var namePosition = _current.Position;
        var name = ExpectIdentifier("a trigger name");

        var timing = ParseTriggerTiming();
        var (events, updateOfColumns) = ParseTriggerEvents();

        ExpectKeyword("ON");
        var (schemaName, tableName) = ParseQualifiedName();

        var level = TriggerLevel.Statement;
        if (_current.IsKeyword("FOR"))
        {
            Advance();
            ExpectKeyword("EACH");
            if (_current.IsKeyword("ROW")) { Advance(); level = TriggerLevel.Row; }
            else if (_current.IsKeyword("STATEMENT")) { Advance(); level = TriggerLevel.Statement; }
            else { throw Error($"Expected ROW or STATEMENT after FOR EACH, found '{_current.Text}'."); }
        }

        string? when = null;
        if (_current.IsKeyword("WHEN"))
        {
            Advance();
            when = ReadRawExpression(parenthesised: true);
        }

        ExpectKeyword("EXECUTE");
        if (_current.IsKeyword("FUNCTION") || _current.IsKeyword("PROCEDURE"))
        {
            Advance();
        }
        else
        {
            throw Error($"Expected FUNCTION or PROCEDURE after EXECUTE, found '{_current.Text}'.");
        }

        var function = ExpectIdentifier("a function name");
        if (Match(TokenKind.Dot))
        {
            function += "." + ExpectIdentifier("a function name");
        }

        // The argument list is captured verbatim (opaque), like a routine's; usually empty for a trigger function.
        _lexer.ResetTo(_current.Position);
        var arguments = _lexer.ReadParenthesizedExpression();
        _current = _lexer.Next();

        Expect(TokenKind.Semicolon, "';'");

        var trigger = new Trigger(name, timing, events, function, level, updateOfColumns,
            when, string.IsNullOrEmpty(arguments) ? null : arguments, doc);
        schemas.AddTrigger(schemaName, tableName, trigger, namePosition);
    }

    private TriggerTiming ParseTriggerTiming()
    {
        if (_current.IsKeyword("BEFORE")) { Advance(); return TriggerTiming.Before; }
        if (_current.IsKeyword("AFTER")) { Advance(); return TriggerTiming.After; }
        if (_current.IsKeyword("INSTEAD")) { Advance(); ExpectKeyword("OF"); return TriggerTiming.InsteadOf; }
        throw Error($"Expected BEFORE, AFTER or INSTEAD OF, found '{_current.Text}'.");
    }

    private (TriggerEvent Events, List<string>? UpdateOfColumns) ParseTriggerEvents()
    {
        var events = TriggerEvent.None;
        List<string>? updateOfColumns = null;
        while (true)
        {
            var position = _current.Position;
            if (_current.IsKeyword("INSERT"))
            {
                Advance();
                AddEvent(TriggerEvent.Insert, position);
            }
            else if (_current.IsKeyword("DELETE"))
            {
                Advance();
                AddEvent(TriggerEvent.Delete, position);
            }
            else if (_current.IsKeyword("TRUNCATE"))
            {
                Advance();
                AddEvent(TriggerEvent.Truncate, position);
            }
            else if (_current.IsKeyword("UPDATE"))
            {
                Advance();
                AddEvent(TriggerEvent.Update, position);
                if (_current.IsKeyword("OF"))
                {
                    Advance();
                    updateOfColumns = ParseColumnList();
                }
            }
            else
            {
                throw Error($"Expected INSERT, UPDATE, DELETE or TRUNCATE, found '{_current.Text}'.");
            }

            if (!_current.IsKeyword("OR"))
            {
                return (events, updateOfColumns);
            }
            Advance(); // OR
        }

        void AddEvent(TriggerEvent next, SourcePosition position)
        {
            if (events.HasFlag(next))
            {
                throw new DdlSyntaxException($"Trigger event '{next.ToString().ToUpperInvariant()}' is specified more than once.", position);
            }
            events |= next;
        }
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
        else if (_current.IsKeyword("VIEW") || _current.IsKeyword("MATERIALIZED"))
        {
            // DROP VIEW and DROP MATERIALIZED VIEW both record a dropped view (the kind is resolved from the
            // current state when the drop is planned).
            if (Advance().IsKeyword("MATERIALIZED"))
            {
                ExpectKeyword("VIEW");
            }
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
        else if (_current.IsKeyword("FUNCTION") || _current.IsKeyword("PROCEDURE") || _current.IsKeyword("ROUTINE"))
        {
            // DROP FUNCTION / DROP PROCEDURE / DROP ROUTINE all record a dropped routine (the kind is resolved
            // from the current state when the drop is planned), since they share one name space.
            Advance();
            var (schema, routine) = ParseQualifiedName();
            Expect(TokenKind.Semicolon, "';'");
            schemas.DropRoutine(schema, routine);
        }
        else if (_current.IsKeyword("EXTENSION"))
        {
            Advance();
            var name = ParseExtensionName();
            Expect(TokenKind.Semicolon, "';'");
            schemas.DropExtension(name);
        }
        else
        {
            throw Error($"Expected SCHEMA, TABLE, VIEW, ENUM, SEQUENCE, FUNCTION, PROCEDURE, ROUTINE or EXTENSION after DROP, found '{_current.Text}'.");
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
        private readonly List<Extension> _extensions = [];
        private readonly List<string> _droppedExtensions = [];
        private readonly List<PendingGrant> _tableGrants = [];
        private readonly List<PendingTrigger> _triggers = [];
        private readonly List<PendingIndex> _standaloneIndexes = [];

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

        // Functions and procedures are one routine pool sharing a single name space, as they do in the database:
        // a function and a procedure with the same name cannot coexist in a schema, which a single list enforces.
        public void AddRoutine(string schema, Routine routine, SourcePosition position)
        {
            var entry = GetOrAdd(schema);
            if (entry.Routines.Any(r => string.Equals(r.Name, routine.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new DdlSyntaxException(
                    $"Routine '{schema}.{routine.Name}' is already declared (functions and procedures share one name space).", position);
            }
            entry.Routines.Add(routine);
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

        // Triggers are standalone statements attached to their table at Build, so a trigger may appear before or
        // after the CREATE TABLE it targets.
        public void AddTrigger(string schema, string table, Trigger trigger, SourcePosition position)
            => _triggers.Add(new PendingTrigger(schema, table, trigger, position));

        // Standalone indexes attach to their relation (a table or a materialized view) at Build, so an index may
        // appear before or after the CREATE that declares its target.
        public void AddIndex(string schema, string relation, TableIndex index, SourcePosition position)
            => _standaloneIndexes.Add(new PendingIndex(schema, relation, index, position));

        // Extensions are database-global, so they live on the accumulator itself rather than a per-schema entry.
        public void AddExtension(Extension extension, SourcePosition position)
        {
            if (_extensions.Any(e => string.Equals(e.Name, extension.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new DdlSyntaxException($"Extension '{extension.Name}' is already declared.", position);
            }
            _extensions.Add(extension);
        }

        public void DropSchema(string name)
        {
            if (!_droppedSchemas.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                _droppedSchemas.Add(name);
            }
        }

        public void DropExtension(string name)
        {
            if (!_droppedExtensions.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                _droppedExtensions.Add(name);
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

        public void DropRoutine(string schema, string routine)
        {
            var entry = GetOrAdd(schema);
            if (!entry.DroppedRoutines.Contains(routine, StringComparer.OrdinalIgnoreCase))
            {
                entry.DroppedRoutines.Add(routine);
            }
        }

        public DatabaseSchema Build()
        {
            ApplyTableGrants();
            ApplyTriggers();
            ApplyIndexes();
            var schemas = _entries
                .Select(e => new SchemaDefinition(e.Name, e.OldName, e.IsPartial, e.Comment, e.Tables, e.DroppedTables, e.Grants, e.Views, e.DroppedViews,
                    e.Enums, e.DroppedEnums, e.Sequences, e.DroppedSequences,
                    e.Routines, e.DroppedRoutines))
                .ToList();
            return new DatabaseSchema(schemas, _droppedSchemas, _extensions, _droppedExtensions);
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

        private void ApplyTriggers()
        {
            foreach (var pending in _triggers)
            {
                if (!_byName.TryGetValue(pending.Schema, out var entry))
                {
                    throw new DdlSyntaxException($"CREATE TRIGGER references unknown table '{pending.Schema}.{pending.Table}'.", pending.Position);
                }

                var index = entry.Tables.FindIndex(t => t.Name == pending.Table);
                if (index < 0)
                {
                    throw new DdlSyntaxException($"CREATE TRIGGER references unknown table '{pending.Schema}.{pending.Table}'.", pending.Position);
                }

                var table = entry.Tables[index];
                if (table.Triggers.Any(t => string.Equals(t.Name, pending.Trigger.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new DdlSyntaxException($"Trigger '{pending.Trigger.Name}' on '{pending.Schema}.{pending.Table}' is already declared.", pending.Position);
                }

                entry.Tables[index] = table with { Triggers = [.. table.Triggers, pending.Trigger] };
            }
        }

        private void ApplyIndexes()
        {
            foreach (var pending in _standaloneIndexes)
            {
                var qualified = $"{pending.Schema}.{pending.Relation}";
                if (!_byName.TryGetValue(pending.Schema, out var entry))
                {
                    throw new DdlSyntaxException($"CREATE INDEX references unknown table or materialized view '{qualified}'.", pending.Position);
                }

                // A standalone index attaches to a table (the same as an inline index) or a materialized view.
                var tableIndex = entry.Tables.FindIndex(t => t.Name == pending.Relation);
                if (tableIndex >= 0)
                {
                    var table = entry.Tables[tableIndex];
                    if (table.Indexes.Any(i => string.Equals(i.Name, pending.Index.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new DdlSyntaxException($"Index '{pending.Index.Name}' on '{qualified}' is already declared.", pending.Position);
                    }
                    entry.Tables[tableIndex] = table with { Indexes = [.. table.Indexes, pending.Index] };
                    continue;
                }

                var viewIndex = entry.Views.FindIndex(v => v.Name == pending.Relation);
                if (viewIndex < 0)
                {
                    throw new DdlSyntaxException($"CREATE INDEX references unknown table or materialized view '{qualified}'.", pending.Position);
                }

                var view = entry.Views[viewIndex];
                if (!view.IsMaterialized)
                {
                    throw new DdlSyntaxException($"CREATE INDEX targets '{qualified}', which is not a materialized view (a plain view cannot be indexed).", pending.Position);
                }
                if (view.Indexes.Any(i => string.Equals(i.Name, pending.Index.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new DdlSyntaxException($"Index '{pending.Index.Name}' on '{qualified}' is already declared.", pending.Position);
                }

                entry.Views[viewIndex] = view with { Indexes = [.. view.Indexes, pending.Index] };
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
            public List<Routine> Routines { get; } = [];
            public List<string> DroppedRoutines { get; } = [];
        }

        private readonly record struct PendingGrant(string Schema, string Table, TableGrant Grant, SourcePosition Position);

        private readonly record struct PendingTrigger(string Schema, string Table, Trigger Trigger, SourcePosition Position);

        private readonly record struct PendingIndex(string Schema, string Relation, TableIndex Index, SourcePosition Position);
    }
}
