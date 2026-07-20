using NSchema.Model;
using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Syntax.CompositeTypes;
using NSchema.Project.Nsql.Syntax.Constraints;
using NSchema.Project.Nsql.Syntax.Domains;
using NSchema.Project.Nsql.Syntax.Enums;
using NSchema.Project.Nsql.Syntax.Extensions;
using NSchema.Project.Nsql.Syntax.Indexes;
using NSchema.Project.Nsql.Syntax.Routines;
using NSchema.Project.Nsql.Syntax.Schemas;
using NSchema.Project.Nsql.Syntax.Sequences;
using NSchema.Project.Nsql.Syntax.Tables;
using NSchema.Project.Nsql.Syntax.Templates;
using NSchema.Project.Nsql.Syntax.Triggers;
using NSchema.Project.Nsql.Syntax.Views;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql;

internal sealed partial class NsqlParser
{
    private NsqlStatement ParseCreate(string? doc)
    {
        var position = _current.Position;
        Advance(); // CREATE

        if (_current.IsKeyword(NsqlKeywords.Schema))
        {
            if (_inTemplateBody)
            {
                throw Error("CREATE SCHEMA is not supported inside a template; apply the template to existing schemas instead.");
            }
            return ParseCreateSchema(position, doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Table))
        {
            return ParseCreateTable(position, doc);
        }
        if (_current.IsKeyword(NsqlKeywords.View))
        {
            if (_inTemplateBody)
            {
                throw Error("CREATE VIEW is not supported inside a template: a view body is opaque, so its references cannot be re-pointed at each target schema.");
            }
            Advance(); // VIEW
            return ParseCreateView(position, doc, materialized: false);
        }
        if (_current.IsKeyword(NsqlKeywords.Materialized))
        {
            if (_inTemplateBody)
            {
                throw Error("CREATE MATERIALIZED VIEW is not supported inside a template: a view body is opaque, so its references cannot be re-pointed at each target schema.");
            }
            Advance(); // MATERIALIZED
            ExpectKeyword(NsqlKeywords.View);
            return ParseCreateView(position, doc, materialized: true);
        }
        if (_current.IsKeyword(NsqlKeywords.Enum))
        {
            return ParseCreateEnum(position, doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Domain))
        {
            return ParseCreateDomain(position, doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Type))
        {
            return ParseCreateCompositeType(position, doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Sequence))
        {
            return ParseCreateSequence(position, doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Function))
        {
            return ParseCreateRoutine(position, doc, RoutineKind.Function);
        }
        if (_current.IsKeyword(NsqlKeywords.Procedure))
        {
            return ParseCreateRoutine(position, doc, RoutineKind.Procedure);
        }
        if (_current.IsKeyword(NsqlKeywords.Extension))
        {
            if (_inTemplateBody)
            {
                throw Error("CREATE EXTENSION is not supported inside a template; extensions are database-global.");
            }
            return ParseCreateExtension(position, doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Trigger))
        {
            return ParseCreateTrigger(position, doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Index))
        {
            return ParseCreateIndex(position, doc, unique: false);
        }
        if (_current.IsKeyword(NsqlKeywords.Unique))
        {
            Advance(); // UNIQUE
            return ParseCreateIndex(position, doc, unique: true);
        }
        throw Error($"Expected SCHEMA, TABLE, VIEW, MATERIALIZED VIEW, ENUM, DOMAIN, TYPE, SEQUENCE, FUNCTION, PROCEDURE, EXTENSION, TRIGGER or INDEX after CREATE, found '{_current.Text}'.");
    }

    private CreateSchemaStatement ParseCreateSchema(SourcePosition position, string? doc)
    {
        Advance(); // SCHEMA
        var name = ExpectIdentifierNode("a schema name");
        Expect(TokenKind.Semicolon, "';'");
        return new CreateSchemaStatement(name) { Position = position, Doc = doc };
    }

    private CreateTableStatement ParseCreateTable(SourcePosition position, string? doc)
    {
        Advance(); // TABLE
        var name = ParseQualifiedNameNode();

        Expect(TokenKind.LeftParen, "'(' to begin the table body");
        var members = new List<TableMember>();
        var primaryKeySeen = false;
        do
        {
            var itemDoc = TakePendingDoc();
            members.Add(ParseTableItem(itemDoc, ref primaryKeySeen));
        }
        while (Match(TokenKind.Comma));
        Expect(TokenKind.RightParen, "')' or ',' after a table member");
        Expect(TokenKind.Semicolon, "';'");

        return new CreateTableStatement(name, members) { Position = position, Doc = doc };
    }

    // The "VIEW" keyword has already been consumed by the dispatcher (preceded by "MATERIALIZED" when
    // <paramref name="materialized"/> is true).
    private CreateViewStatement ParseCreateView(SourcePosition position, string? doc, bool materialized)
    {
        var name = ParseQualifiedNameNode();
        ExpectKeyword(NsqlKeywords.As);

        // The body is captured verbatim; projection derives the view's dependencies from it.
        var body = CaptureRawSpan("a view body", [TokenKind.Semicolon]);
        Expect(TokenKind.Semicolon, "';' to end the view definition");

        return new CreateViewStatement(name, body, materialized) { Position = position, Doc = doc };
    }

    /// <summary>
    /// Parses a standalone index: <c>CREATE [UNIQUE] INDEX name ON s.relation (cols) [WHERE (expr)]</c>. The
    /// "UNIQUE" keyword (if present) has already been consumed by the dispatcher.
    /// </summary>
    private CreateIndexStatement ParseCreateIndex(SourcePosition position, string? doc, bool unique)
    {
        ExpectKeyword(NsqlKeywords.Index);
        var name = ExpectIdentifierNode("an index name");
        ExpectKeyword(NsqlKeywords.On);
        var on = ParseQualifiedNameNode();
        var method = TryParseIndexMethodNode();
        var columns = ParseIndexColumns();
        var include = TryParseIncludeColumns();
        var predicate = TryParseWherePredicate();
        Expect(TokenKind.Semicolon, "';'");

        return new CreateIndexStatement(name, unique, on, columns, method, include, predicate) { Position = position, Doc = doc };
    }

    private CreateRoutineStatement ParseCreateRoutine(SourcePosition position, string? doc, RoutineKind kind)
    {
        Advance(); // FUNCTION | PROCEDURE
        var what = kind == RoutineKind.Procedure ? "a procedure definition" : "a function definition";
        var name = ParseQualifiedNameNode();
        var arguments = CaptureParenthesized();
        var definition = CaptureRawSpan(what, [TokenKind.Semicolon]);
        Expect(TokenKind.Semicolon, $"';' to end {what}");

        return new CreateRoutineStatement(name, kind, arguments, definition) { Position = position, Doc = doc };
    }

    private CreateEnumStatement ParseCreateEnum(SourcePosition position, string? doc)
    {
        Advance(); // ENUM
        var name = ParseQualifiedNameNode();

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
                    throw new NsqlSyntaxException($"Enum value '{value}' is declared more than once.", valuePosition);
                }
                values.Add(value);
            }
            while (Match(TokenKind.Comma));
        }
        Expect(TokenKind.RightParen, "')' or ',' after an enum value");
        Expect(TokenKind.Semicolon, "';'");

        return new CreateEnumStatement(name, values) { Position = position, Doc = doc };
    }

    /// <summary>
    /// Parses a domain: <c>CREATE DOMAIN s.d AS &lt;type&gt; [NOT NULL | NULL] [CONSTRAINT n CHECK (e)]… [DEFAULT expr]</c>.
    /// The optional <c>DEFAULT</c> clause, if present, must come last: its expression is opaque and read up to the
    /// terminating <c>;</c>.
    /// </summary>
    private CreateDomainStatement ParseCreateDomain(SourcePosition position, string? doc)
    {
        Advance(); // DOMAIN
        var name = ParseQualifiedNameNode();
        ExpectKeyword(NsqlKeywords.As);
        var dataType = ParseTypeNode();

        SqlText? @default = null;
        var notNull = false;
        var checks = new List<CheckDefinition>();

        while (@default is null && _current.Kind != TokenKind.Semicolon)
        {
            if (_current.IsKeyword(NsqlKeywords.Not))
            {
                Advance();
                ExpectKeyword(NsqlKeywords.Null);
                notNull = true;
            }
            else if (_current.IsKeyword(NsqlKeywords.Null))
            {
                Advance();
                notNull = false;
            }
            else if (_current.IsKeyword(NsqlKeywords.Constraint))
            {
                var checkPosition = _current.Position;
                Advance();
                var checkName = ExpectIdentifierNode("a constraint name");
                ExpectKeyword(NsqlKeywords.Check);
                checks.Add(new CheckDefinition(checkName, ReadRawExpression(parenthesised: true)) { Position = checkPosition });
            }
            else if (_current.IsKeyword(NsqlKeywords.Default))
            {
                Advance();
                // The default is opaque and read to the terminating ';', so it must be the final clause.
                @default = CaptureRawSpan("a domain default", [TokenKind.Semicolon]);
            }
            else
            {
                throw Error($"Expected NOT NULL, NULL, CONSTRAINT … CHECK, DEFAULT or ';', found '{_current.Text}'.");
            }
        }

        Expect(TokenKind.Semicolon, "';'");

        return new CreateDomainStatement(name, dataType, notNull, checks, @default) { Position = position, Doc = doc };
    }

    /// <summary>
    /// Parses a composite type: <c>CREATE TYPE s.t AS (field &lt;type&gt;, field &lt;type&gt;, …)</c>.
    /// </summary>
    private CreateCompositeTypeStatement ParseCreateCompositeType(SourcePosition position, string? doc)
    {
        Advance(); // TYPE
        var name = ParseQualifiedNameNode();
        ExpectKeyword(NsqlKeywords.As);
        Expect(TokenKind.LeftParen, "'(' to begin the composite type fields");

        var fields = new List<CompositeFieldDefinition>();
        if (_current.Kind != TokenKind.RightParen)
        {
            do
            {
                var fieldName = ExpectIdentifierNode("a field name");
                var fieldType = ParseTypeNode();
                fields.Add(new CompositeFieldDefinition(fieldName, fieldType) { Position = fieldName.Position });
            }
            while (Match(TokenKind.Comma));
        }

        Expect(TokenKind.RightParen, "')' or ',' after a composite type field");
        Expect(TokenKind.Semicolon, "';'");

        return new CreateCompositeTypeStatement(name, fields) { Position = position, Doc = doc };
    }

    private CreateSequenceStatement ParseCreateSequence(SourcePosition position, string? doc)
    {
        Advance(); // SEQUENCE
        var name = ParseQualifiedNameNode();
        var options = TryParseSequenceOptions();
        Expect(TokenKind.Semicolon, "';'");

        return new CreateSequenceStatement(name, options) { Position = position, Doc = doc };
    }

    private SequenceOptionsClause? TryParseSequenceOptions()
    {
        if (_current.Kind != TokenKind.LeftParen)
        {
            return null;
        }
        var clausePosition = _current.Position;
        Advance();

        TypeName? dataType = null;
        long? start = null, increment = null, min = null, max = null, cache = null;
        var cycle = false;
        do
        {
            var optionPosition = _current.Position;
            var option = ExpectIdentifierNode("a sequence option");

            void RejectDuplicate(bool alreadySet)
            {
                if (alreadySet)
                {
                    throw new NsqlSyntaxException($"Sequence option '{option.Value.ToUpperInvariant()}' is specified more than once.", optionPosition);
                }
            }

            if (NsqlKeywords.Comparer.Equals(option.Value, NsqlKeywords.As))
            {
                RejectDuplicate(dataType is not null);
                var typeName = ExpectIdentifierNode("a type name");
                dataType = new TypeName(null, typeName) { Position = typeName.Position };
            }
            else if (NsqlKeywords.Comparer.Equals(option.Value, NsqlKeywords.Start))
            {
                RejectDuplicate(start is not null);
                start = ExpectSignedIntegerValue();
            }
            else if (NsqlKeywords.Comparer.Equals(option.Value, NsqlKeywords.Increment))
            {
                RejectDuplicate(increment is not null);
                increment = ExpectSignedIntegerValue();
            }
            else if (NsqlKeywords.Comparer.Equals(option.Value, NsqlKeywords.MinValue))
            {
                RejectDuplicate(min is not null);
                min = ExpectSignedIntegerValue();
            }
            else if (NsqlKeywords.Comparer.Equals(option.Value, NsqlKeywords.MaxValue))
            {
                RejectDuplicate(max is not null);
                max = ExpectSignedIntegerValue();
            }
            else if (NsqlKeywords.Comparer.Equals(option.Value, NsqlKeywords.Cache))
            {
                RejectDuplicate(cache is not null);
                cache = ExpectSignedIntegerValue();
            }
            else if (NsqlKeywords.Comparer.Equals(option.Value, NsqlKeywords.Cycle))
            {
                RejectDuplicate(cycle);
                cycle = true;
            }
            else
            {
                throw new NsqlSyntaxException(
                    $"Unknown sequence option '{option.Value}'; expected {NsqlKeywords.As}, {NsqlKeywords.Start}, {NsqlKeywords.Increment}, {NsqlKeywords.MinValue}, {NsqlKeywords.MaxValue}, {NsqlKeywords.Cache} or {NsqlKeywords.Cycle}.", optionPosition);
            }
        }
        while (Match(TokenKind.Comma));
        Expect(TokenKind.RightParen, "')'");

        return new SequenceOptionsClause(dataType, start, increment, min, max, cache, cycle) { Position = clausePosition };
    }

    private CreateExtensionStatement ParseCreateExtension(SourcePosition position, string? doc)
    {
        Advance(); // EXTENSION
        var name = ParseExtensionNameNode();
        string? version = null;
        if (_current.IsKeyword(NsqlKeywords.Version))
        {
            Advance();
            version = Expect(TokenKind.String, "a version string after VERSION").Text;
        }
        Expect(TokenKind.Semicolon, "';'");

        return new CreateExtensionStatement(name, version) { Position = position, Doc = doc };
    }

    /// <summary>
    /// Reads an extension name, which may be a bare identifier (<c>citext</c>) or a quoted string
    /// (<c>'uuid-ossp'</c>) — extension names commonly contain characters, such as a hyphen, that a bare
    /// identifier cannot.
    /// </summary>
    private Identifier ParseExtensionNameNode()
    {
        if (_current.Kind == TokenKind.String)
        {
            var token = Advance();
            return new Identifier(token.Text) { Position = token.Position };
        }
        return ExpectIdentifierNode("an extension name");
    }

    /// <summary>
    /// Parses a standalone trigger: <c>CREATE TRIGGER name {BEFORE|AFTER|INSTEAD OF} event {OR event} ON s.t
    /// [FOR EACH {ROW|STATEMENT}] [WHEN (expr)] {EXECUTE {FUNCTION|PROCEDURE} f(args) | AS $$ body $$}</c>.
    /// </summary>
    private CreateTriggerStatement ParseCreateTrigger(SourcePosition position, string? doc)
    {
        Advance(); // TRIGGER
        var name = ExpectIdentifierNode("a trigger name");

        var timing = ParseTriggerTiming();
        var (events, updateOfColumns) = ParseTriggerEvents();

        ExpectKeyword(NsqlKeywords.On);
        var on = ParseQualifiedNameNode();

        var level = TriggerLevel.Statement;
        if (_current.IsKeyword(NsqlKeywords.For))
        {
            Advance();
            ExpectKeyword(NsqlKeywords.Each);
            if (_current.IsKeyword(NsqlKeywords.Row)) { Advance(); level = TriggerLevel.Row; }
            else if (_current.IsKeyword(NsqlKeywords.Statement)) { Advance(); level = TriggerLevel.Statement; }
            else { throw Error($"Expected ROW or STATEMENT after FOR EACH, found '{_current.Text}'."); }
        }

        SqlText? when = null;
        if (_current.IsKeyword(NsqlKeywords.When))
        {
            Advance();
            when = ReadRawExpression(parenthesised: true);
        }

        TriggerAction action;
        var actionPosition = _current.Position;
        if (_current.IsKeyword(NsqlKeywords.Execute))
        {
            Advance();
            if (_current.IsAnyKeyword(NsqlKeywords.Function, NsqlKeywords.Procedure))
            {
                Advance();
            }
            else
            {
                throw Error($"Expected FUNCTION or PROCEDURE after EXECUTE, found '{_current.Text}'.");
            }

            var first = ExpectIdentifierNode("a function name");
            var function = Match(TokenKind.Dot)
                ? new QualifiedName(first, ExpectIdentifierNode("a function name")) { Position = first.Position }
                : new QualifiedName(null, first) { Position = first.Position };

            // The argument list is captured verbatim (opaque), like a routine's; usually empty for a trigger function.
            var arguments = CaptureParenthesized();
            action = new ExecuteFunctionAction(function, arguments) { Position = actionPosition };
        }
        else if (_current.IsKeyword(NsqlKeywords.As))
        {
            // An inline body is a dollar-quoted block (so it may contain its own ';'), like a deployment script.
            Advance();
            var dollar = Expect(TokenKind.DollarString, "a trigger body as a dollar-quoted block ($$ … $$)");
            action = new InlineBodyAction(StripDollarQuote(dollar.Text).Trim()) { Position = actionPosition };
        }
        else
        {
            throw Error($"Expected EXECUTE or AS to begin the trigger action, found '{_current.Text}'.");
        }

        Expect(TokenKind.Semicolon, "';'");

        return new CreateTriggerStatement(name, timing, events, on, action, updateOfColumns, level, when) { Position = position, Doc = doc };
    }

    private TriggerTiming ParseTriggerTiming()
    {
        if (_current.IsKeyword(NsqlKeywords.Before)) { Advance(); return TriggerTiming.Before; }
        if (_current.IsKeyword(NsqlKeywords.After)) { Advance(); return TriggerTiming.After; }
        if (_current.IsKeyword(NsqlKeywords.Instead)) { Advance(); ExpectKeyword(NsqlKeywords.Of); return TriggerTiming.InsteadOf; }
        throw Error($"Expected BEFORE, AFTER or INSTEAD OF, found '{_current.Text}'.");
    }

    private (TriggerEvent Events, List<Identifier>? UpdateOfColumns) ParseTriggerEvents()
    {
        var events = TriggerEvent.None;
        List<Identifier>? updateOfColumns = null;
        while (true)
        {
            var position = _current.Position;
            if (_current.IsKeyword(NsqlKeywords.Insert))
            {
                Advance();
                AddEvent(TriggerEvent.Insert, position);
            }
            else if (_current.IsKeyword(NsqlKeywords.Delete))
            {
                Advance();
                AddEvent(TriggerEvent.Delete, position);
            }
            else if (_current.IsKeyword(NsqlKeywords.Truncate))
            {
                Advance();
                AddEvent(TriggerEvent.Truncate, position);
            }
            else if (_current.IsKeyword(NsqlKeywords.Update))
            {
                Advance();
                AddEvent(TriggerEvent.Update, position);
                if (_current.IsKeyword(NsqlKeywords.Of))
                {
                    Advance();
                    updateOfColumns = ParseColumnListNodes();
                }
            }
            else
            {
                throw Error($"Expected INSERT, UPDATE, DELETE or TRUNCATE, found '{_current.Text}'.");
            }

            if (!_current.IsKeyword(NsqlKeywords.Or))
            {
                return (events, updateOfColumns);
            }
            Advance(); // OR
        }

        void AddEvent(TriggerEvent next, SourcePosition position)
        {
            if (events.HasFlag(next))
            {
                throw new NsqlSyntaxException($"Trigger event '{next.ToString().ToUpperInvariant()}' is specified more than once.", position);
            }
            events |= next;
        }
    }

    private TableMember ParseTableItem(string? doc, ref bool primaryKeySeen)
    {
        var member = ParseTableItemCore(doc);
        if (member is PrimaryKeyDefinition)
        {
            if (primaryKeySeen)
            {
                throw Error("A table may declare only one primary key.");
            }
            primaryKeySeen = true;
        }
        return member;
    }

    private TableMember ParseTableItemCore(string? doc)
    {
        if (_current.IsKeyword(NsqlKeywords.Constraint))
        {
            return ParseConstraint(doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Unique))
        {
            return ParseIndexMember(doc, isUnique: true);
        }
        if (_current.IsKeyword(NsqlKeywords.Index))
        {
            return ParseIndexMember(doc, isUnique: false);
        }
        if (_current.IsKeyword(NsqlKeywords.Include))
        {
            return ParseIncludeOrColumn(doc);
        }
        if (_current.IsName)
        {
            return ParseColumn(doc);
        }
        throw Error("Expected a column or constraint definition.");
    }

    /// <summary>
    /// Disambiguates a member starting with <c>INCLUDE</c>: it is a template include only when nothing follows
    /// the name (the member ends at <c>,</c> or <c>)</c>); otherwise it is a column named <c>include</c> whose
    /// type is the identifier — the pre-template reading, kept so existing DDL parses unchanged.
    /// </summary>
    private TableMember ParseIncludeOrColumn(string? doc)
    {
        var include = Advance(); // INCLUDE (or a column named 'include')

        if (_current.IsName
            && _lexer.Peek().Kind is TokenKind.Comma or TokenKind.RightParen)
        {
            if (_inTableTemplateBody)
            {
                throw Error("INCLUDE is not supported inside a table template; a table template cannot include another.");
            }
            var templateName = ExpectIdentifierNode("a template name");
            return new IncludeMember(templateName) { Position = include.Position, Doc = doc };
        }

        return ParseColumn(new Identifier(include.Text) { Position = include.Position }, doc);
    }

    private TableMember ParseColumn(string? doc)
        => ParseColumn(ExpectIdentifierNode("a column name"), doc);

    private TableMember ParseColumn(Identifier name, string? doc)
    {
        var type = ParseTypeNode();

        var isNullable = true;
        if (_current.IsKeyword(NsqlKeywords.Not))
        {
            Advance();
            ExpectKeyword(NsqlKeywords.Null);
            isNullable = false;
        }
        else if (_current.IsKeyword(NsqlKeywords.Null))
        {
            Advance();
        }

        var isIdentity = false;
        IdentityOptionsClause? identity = null;
        if (_current.IsKeyword(NsqlKeywords.Identity))
        {
            Advance();
            isIdentity = true;
            identity = TryParseIdentityOptions();
        }

        SqlText? defaultExpression = null;
        if (_current.IsKeyword(NsqlKeywords.Default))
        {
            Advance();
            defaultExpression = ReadRawExpression(parenthesised: false);
        }

        SqlText? generatedExpression = null;
        if (_current.IsKeyword(NsqlKeywords.Generated))
        {
            Advance();
            ExpectKeyword(NsqlKeywords.Always);
            ExpectKeyword(NsqlKeywords.As);
            generatedExpression = ReadRawExpression(parenthesised: true);
            ExpectKeyword(NsqlKeywords.Stored);
        }


        return new ColumnDefinition(name, type, isNullable, isIdentity, identity, defaultExpression, generatedExpression)
        {
            Position = name.Position,
            Doc = doc,
        };
    }

    private TypeName ParseTypeNode()
    {
        var first = ExpectIdentifierNode("a column type");
        Identifier? schema = null;
        var name = first;
        if (Match(TokenKind.Dot))
        {
            // A schema-qualified user-defined type, e.g. an enum referenced as `app.status`.
            schema = first;
            name = ExpectIdentifierNode("a schema-qualified type name");
        }
        string? arguments = null;
        if (_current.Kind == TokenKind.LeftParen)
        {
            Advance();
            arguments = ExpectInteger();
            if (Match(TokenKind.Comma))
            {
                arguments += "," + ExpectInteger();
            }
            Expect(TokenKind.RightParen, "')'");
        }
        return new TypeName(schema, name, arguments) { Position = first.Position };
    }

    private IdentityOptionsClause? TryParseIdentityOptions()
    {
        if (_current.Kind != TokenKind.LeftParen)
        {
            return null;
        }
        var clausePosition = _current.Position;
        Advance();

        long? start = null, min = null, increment = null;
        do
        {
            var option = ExpectIdentifierNode($"{NsqlKeywords.Start}, {NsqlKeywords.Increment} or {NsqlKeywords.MinValue}");
            var value = ExpectIntegerValue();
            if (NsqlKeywords.Comparer.Equals(option.Value, NsqlKeywords.Start))
            {
                start = value;
            }
            else if (NsqlKeywords.Comparer.Equals(option.Value, NsqlKeywords.Increment))
            {
                increment = value;
            }
            else if (NsqlKeywords.Comparer.Equals(option.Value, NsqlKeywords.MinValue))
            {
                min = value;
            }
            else
            {
                throw Error($"Unknown identity option '{option.Value}'; expected {NsqlKeywords.Start}, {NsqlKeywords.Increment} or {NsqlKeywords.MinValue}.");
            }
        }
        while (Match(TokenKind.Comma));
        Expect(TokenKind.RightParen, "')'");

        return new IdentityOptionsClause(start, increment, min) { Position = clausePosition };
    }

    private TableMember ParseConstraint(string? doc)
    {
        var position = _current.Position;
        Advance(); // CONSTRAINT
        var name = ExpectIdentifierNode("a constraint name");

        if (_current.IsKeyword(NsqlKeywords.Primary))
        {
            Advance();
            ExpectKeyword(NsqlKeywords.Key);
            var columns = ParseColumnListNodes();
            return new PrimaryKeyDefinition(name, columns) { Position = position, Doc = doc };
        }
        if (_current.IsKeyword(NsqlKeywords.Foreign))
        {
            Advance();
            ExpectKeyword(NsqlKeywords.Key);
            var columns = ParseColumnListNodes();
            ExpectKeyword(NsqlKeywords.References);
            var references = ParseQualifiedNameNode();
            var refColumns = ParseColumnListNodes();
            var (onDelete, onUpdate) = ParseReferentialActions();
            return new ForeignKeyDefinition(name, columns, references, refColumns, onDelete, onUpdate) { Position = position, Doc = doc };
        }
        if (_current.IsKeyword(NsqlKeywords.Unique))
        {
            Advance();
            var columns = ParseColumnListNodes();
            return new UniqueDefinition(name, columns) { Position = position, Doc = doc };
        }
        if (_current.IsKeyword(NsqlKeywords.Check))
        {
            Advance();
            var expression = ReadRawExpression(parenthesised: true);
            return new CheckDefinition(name, expression) { Position = position, Doc = doc };
        }
        if (_current.IsKeyword(NsqlKeywords.Exclude))
        {
            Advance();
            return ParseExclusion(position, name, doc);
        }
        throw Error($"Expected PRIMARY KEY, FOREIGN KEY, UNIQUE, CHECK or EXCLUDE, found '{_current.Text}'.");
    }

    private (ReferentialAction OnDelete, ReferentialAction OnUpdate) ParseReferentialActions()
    {
        var onDelete = ReferentialAction.NoAction;
        var onUpdate = ReferentialAction.NoAction;
        while (_current.IsKeyword(NsqlKeywords.On))
        {
            Advance();
            if (_current.IsKeyword(NsqlKeywords.Delete))
            {
                Advance();
                onDelete = ParseReferentialAction();
            }
            else if (_current.IsKeyword(NsqlKeywords.Update))
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
        if (_current.IsKeyword(NsqlKeywords.No))
        {
            Advance();
            ExpectKeyword(NsqlKeywords.Action);
            return ReferentialAction.NoAction;
        }
        if (_current.IsKeyword(NsqlKeywords.Cascade))
        {
            Advance();
            return ReferentialAction.Cascade;
        }
        if (_current.IsKeyword(NsqlKeywords.Set))
        {
            Advance();
            if (_current.IsKeyword(NsqlKeywords.Null))
            {
                Advance();
                return ReferentialAction.SetNull;
            }
            if (_current.IsKeyword(NsqlKeywords.Default))
            {
                Advance();
                return ReferentialAction.SetDefault;
            }
            throw Error($"Expected NULL or DEFAULT after SET, found '{_current.Text}'.");
        }
        throw Error($"Expected a referential action (NO ACTION, CASCADE, SET NULL, SET DEFAULT), found '{_current.Text}'.");
    }

    /// <summary>
    /// Parses an exclusion constraint body (the <c>EXCLUDE</c> keyword is already consumed):
    /// <c>[USING method] ( element WITH operator [, …] ) [WHERE (predicate)]</c>.
    /// </summary>
    private ExclusionDefinition ParseExclusion(SourcePosition position, Identifier name, string? doc)
    {
        var method = TryParseIndexMethodNode();
        Expect(TokenKind.LeftParen, "'(' to begin the exclusion elements");
        var elements = new List<ExclusionElement> { ParseExclusionElement() };
        while (Match(TokenKind.Comma))
        {
            elements.Add(ParseExclusionElement());
        }
        Expect(TokenKind.RightParen, "')' or ',' after an exclusion element");

        var predicate = TryParseWherePredicate();

        return new ExclusionDefinition(name, elements, method, predicate) { Position = position, Doc = doc };
    }

    private ExclusionElement ParseExclusionElement()
    {
        var position = _current.Position;
        Identifier? column = null;
        SqlText? expression = null;
        if (_current.Kind == TokenKind.LeftParen)
        {
            expression = ReadRawExpression(parenthesised: true);
        }
        else
        {
            column = ExpectIdentifierNode("a column name or expression");
        }

        if (!_current.IsKeyword(NsqlKeywords.With))
        {
            throw Error($"Expected WITH after an exclusion element, found '{_current.Text}'.");
        }

        // The operator is opaque (=, &&, <>, …) and uses characters lexed as Symbol tokens, so "WITH <operator>" is
        // captured verbatim from the WITH keyword up to the element separator or list close, then the leading WITH is
        // stripped.
        var raw = CaptureRawSpan("an exclusion operator", [TokenKind.Comma, TokenKind.RightParen]);
        var @operator = raw.TrimStart()[NsqlKeywords.With.Length..].Trim();

        return new ExclusionElement(@operator, column, expression) { Position = position };
    }

    private IndexDefinition ParseIndexMember(string? doc, bool isUnique)
    {
        var position = _current.Position;
        if (isUnique)
        {
            Advance(); // UNIQUE
        }
        ExpectKeyword(NsqlKeywords.Index);
        var name = ExpectIdentifierNode("an index name");
        var method = TryParseIndexMethodNode();
        var columns = ParseIndexColumns();
        var include = TryParseIncludeColumns();
        var predicate = TryParseWherePredicate();

        return new IndexDefinition(name, isUnique, columns, method, include, predicate) { Position = position, Doc = doc };
    }

    /// <summary>Parses the optional <c>USING &lt;method&gt;</c> clause of an index; returns <see langword="null"/> when absent (default B-tree).</summary>
    private Identifier? TryParseIndexMethodNode()
    {
        if (!_current.IsKeyword(NsqlKeywords.Using))
        {
            return null;
        }
        Advance();
        return ExpectIdentifierNode("an index access method");
    }

    /// <summary>Parses the optional covering <c>INCLUDE (cols)</c> clause of an index; returns an empty list when absent.</summary>
    private List<Identifier> TryParseIncludeColumns()
    {
        if (!_current.IsKeyword(NsqlKeywords.Include))
        {
            return [];
        }
        Advance();
        return ParseColumnListNodes();
    }

    private SqlText? TryParseWherePredicate()
    {
        if (!_current.IsKeyword(NsqlKeywords.Where))
        {
            return null;
        }
        Advance();
        return ReadRawExpression(parenthesised: true);
    }

    /// <summary>
    /// Parses an index key list: each key is a column name or a parenthesised expression, with an optional
    /// <c>ASC</c>/<c>DESC</c> and <c>NULLS FIRST</c>/<c>NULLS LAST</c>.
    /// </summary>
    private List<IndexElement> ParseIndexColumns()
    {
        Expect(TokenKind.LeftParen, "'('");
        var keys = new List<IndexElement> { ParseIndexKey() };
        while (Match(TokenKind.Comma))
        {
            keys.Add(ParseIndexKey());
        }
        Expect(TokenKind.RightParen, "')'");
        return keys;
    }

    private IndexElement ParseIndexKey()
    {
        var position = _current.Position;
        Identifier? column = null;
        SqlText? expression = null;
        if (_current.Kind == TokenKind.LeftParen)
        {
            // A parenthesised expression key, e.g. (lower(email)).
            expression = ReadRawExpression(parenthesised: true);
        }
        else
        {
            column = ExpectIdentifierNode("a column name or expression");
        }

        var sort = IndexSort.Default;
        if (_current.IsKeyword(NsqlKeywords.Asc))
        {
            Advance();
            sort = IndexSort.Ascending;
        }
        else if (_current.IsKeyword(NsqlKeywords.Desc))
        {
            Advance();
            sort = IndexSort.Descending;
        }

        var nulls = IndexNulls.Default;
        if (_current.IsKeyword(NsqlKeywords.Nulls))
        {
            Advance();
            if (_current.IsKeyword(NsqlKeywords.First))
            {
                Advance();
                nulls = IndexNulls.First;
            }
            else if (_current.IsKeyword(NsqlKeywords.Last))
            {
                Advance();
                nulls = IndexNulls.Last;
            }
            else
            {
                throw Error($"Expected FIRST or LAST after NULLS, found '{_current.Text}'.");
            }
        }

        return new IndexElement(column, expression, sort, nulls) { Position = position };
    }

    private List<Identifier> ParseColumnListNodes()
    {
        Expect(TokenKind.LeftParen, "'('");
        var columns = new List<Identifier> { ExpectIdentifierNode("a column name") };
        while (Match(TokenKind.Comma))
        {
            columns.Add(ExpectIdentifierNode("a column name"));
        }
        Expect(TokenKind.RightParen, "')'");
        return columns;
    }

    /// <summary>
    /// Captures an opaque SQL expression as raw text: a balanced <c>( … )</c> when <paramref name="parenthesised"/>
    /// (CHECK / WHERE), or an unparenthesised DEFAULT value otherwise (terminated by the enclosing column list's
    /// <c>,</c> / <c>)</c>).
    /// </summary>
    private SqlText ReadRawExpression(bool parenthesised) => new(
        parenthesised
            ? CaptureParenthesized()
            : CaptureRawSpan("a default expression", [TokenKind.Comma, TokenKind.RightParen]));

}
