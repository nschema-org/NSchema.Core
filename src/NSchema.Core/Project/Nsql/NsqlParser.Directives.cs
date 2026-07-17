using NSchema.Model;
using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Syntax.Schemas;
using NSchema.Project.Nsql.Syntax.Tables;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql;

internal sealed partial class NsqlParser
{
    /// <summary>
    /// Parses a RENAME directive: <c>RENAME &lt;kind&gt; &lt;current&gt; TO &lt;name&gt;;</c>. The current side is
    /// fully qualified (a bare name for SCHEMA, a three-part path for COLUMN); the target is always a bare
    /// name — a rename never moves an object across containers.
    /// </summary>
    private NsqlStatement ParseRename(string? doc)
    {
        var position = _current.Position;
        Advance(); // RENAME

        if (_current.IsKeyword(NsqlKeywords.Schema))
        {
            Advance();
            var from = ExpectIdentifierNode("a schema name");
            var to = ParseRenameTarget("a schema name");
            return new RenameSchemaStatement(from, to) { Position = position, Doc = doc };
        }
        if (_current.IsKeyword(NsqlKeywords.Column))
        {
            Advance();
            var from = ParseColumnPath();
            var to = ParseRenameTarget("a column name");
            return new RenameColumnStatement(from, to) { Position = position, Doc = doc };
        }
        if (TryParseObjectKind() is { } kind)
        {
            var from = ParseQualifiedNameNode();
            var to = ParseRenameTarget(RenameTargetNoun(kind));
            return new RenameObjectStatement(kind, from, to) { Position = position, Doc = doc };
        }

        throw Error("Expected a renameable kind: SCHEMA, TABLE, COLUMN, VIEW, ENUM, DOMAIN, TYPE, SEQUENCE, FUNCTION, PROCEDURE or ROUTINE.");
    }

    /// <summary>
    /// Parses a PARTIAL directive: <c>PARTIAL SCHEMA name;</c>.
    /// </summary>
    private PartialSchemaStatement ParsePartial(string? doc)
    {
        var position = _current.Position;
        Advance(); // PARTIAL
        ExpectKeyword(NsqlKeywords.Schema);
        var schema = ExpectIdentifierNode("a schema name");
        Expect(TokenKind.Semicolon, "';'");
        return new PartialSchemaStatement(schema) { Position = position, Doc = doc };
    }

    /// <summary>
    /// Consumes an object-kind keyword when the current token names a schema-level object kind. VIEW and
    /// MATERIALIZED VIEW both name a view, and FUNCTION, PROCEDURE and ROUTINE all name a routine (they share
    /// one name space) — the concrete kind is resolved from the current state when the directive is planned.
    /// </summary>
    private ObjectKind? TryParseObjectKind()
    {
        if (_current.IsKeyword(NsqlKeywords.Table))
        {
            Advance();
            return ObjectKind.Table;
        }
        if (_current.IsKeyword(NsqlKeywords.View) || _current.IsKeyword(NsqlKeywords.Materialized))
        {
            if (Advance().IsKeyword(NsqlKeywords.Materialized))
            {
                ExpectKeyword(NsqlKeywords.View);
            }
            return ObjectKind.View;
        }
        if (_current.IsKeyword(NsqlKeywords.Enum))
        {
            Advance();
            return ObjectKind.Enum;
        }
        if (_current.IsKeyword(NsqlKeywords.Domain))
        {
            Advance();
            return ObjectKind.Domain;
        }
        if (_current.IsKeyword(NsqlKeywords.Type))
        {
            Advance();
            return ObjectKind.CompositeType;
        }
        if (_current.IsKeyword(NsqlKeywords.Sequence))
        {
            Advance();
            return ObjectKind.Sequence;
        }
        if (_current.IsKeyword(NsqlKeywords.Function) || _current.IsKeyword(NsqlKeywords.Procedure) || _current.IsKeyword(NsqlKeywords.Routine))
        {
            Advance();
            return ObjectKind.Routine;
        }
        return null;
    }

    /// <summary>
    /// What a rename's target should be, for the error when it is missing. The noun follows the keyword the
    /// kind is written as, not the model's name for it (a composite type is written <c>TYPE</c>).
    /// </summary>
    private static string RenameTargetNoun(ObjectKind kind) => kind switch
    {
        ObjectKind.Table => "a table name",
        ObjectKind.View => "a view name",
        ObjectKind.Enum => "an enum name",
        ObjectKind.Domain => "a domain name",
        ObjectKind.CompositeType => "a type name",
        ObjectKind.Sequence => "a sequence name",
        ObjectKind.Routine => "a routine name",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private Identifier ParseRenameTarget(string what)
    {
        ExpectKeyword(NsqlKeywords.To);
        var to = ExpectIdentifierNode(what);
        Expect(TokenKind.Semicolon, "';'");
        return to;
    }

    /// <summary>
    /// Parses a RENAME COLUMN: <c>schema.table.column</c> at the top level, or <c>table.column</c> inside a template.
    /// </summary>
    private MemberPath ParseColumnPath()
    {
        var first = ExpectIdentifierNode("a schema name");
        Expect(TokenKind.Dot, "'.' in the column path");
        var second = ExpectIdentifierNode("a table name");
        if (_inTemplateBody && !Match(TokenKind.Dot))
        {
            // table.column. The schema is decided when the template is applied.
            return new MemberPath(null, first, second) { Position = first.Position };
        }
        Expect(TokenKind.Dot, "'.' in the column path (schema.table.column)");
        var column = ExpectIdentifierNode("a column name");
        return new MemberPath(first, second, column) { Position = first.Position };
    }
}
