using NSchema.Schema.Ddl.Model;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.Constraints;
using NSchema.Schema.Model.Enums;
using NSchema.Schema.Model.Extensions;
using NSchema.Schema.Model.Indexes;
using NSchema.Schema.Model.Routines;
using NSchema.Schema.Model.Sequences;
using NSchema.Schema.Model.Tables;
using NSchema.Schema.Model.Triggers;
using NSchema.Schema.Model.Views;

namespace NSchema.Schema.Ddl;

internal sealed partial class DdlParser
{
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
}
