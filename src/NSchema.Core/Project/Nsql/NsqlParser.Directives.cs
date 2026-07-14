using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Syntax.CompositeTypes;
using NSchema.Project.Nsql.Syntax.Domains;
using NSchema.Project.Nsql.Syntax.Enums;
using NSchema.Project.Nsql.Syntax.Routines;
using NSchema.Project.Nsql.Syntax.Schemas;
using NSchema.Project.Nsql.Syntax.Sequences;
using NSchema.Project.Nsql.Syntax.Tables;
using NSchema.Project.Nsql.Syntax.Views;
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

        if (_current.IsKeyword("SCHEMA"))
        {
            Advance();
            var from = ExpectIdentifierNode("a schema name");
            var to = ParseRenameTarget("a schema name");
            return new RenameSchemaStatement(from, to) { Position = position, Doc = doc };
        }
        if (_current.IsKeyword("COLUMN"))
        {
            Advance();
            var from = ParseColumnPath();
            var to = ParseRenameTarget("a column name");
            return new RenameColumnStatement(from, to) { Position = position, Doc = doc };
        }
        if (_current.IsKeyword("TABLE"))
        {
            Advance();
            var from = ParseQualifiedNameNode();
            var to = ParseRenameTarget("a table name");
            return new RenameTableStatement(from, to) { Position = position, Doc = doc };
        }
        if (_current.IsKeyword("VIEW") || _current.IsKeyword("MATERIALIZED"))
        {
            // RENAME VIEW and RENAME MATERIALIZED VIEW both rename a view (the kind is resolved from the
            // current state), mirroring DROP.
            if (Advance().IsKeyword("MATERIALIZED"))
            {
                ExpectKeyword("VIEW");
            }
            var from = ParseQualifiedNameNode();
            var to = ParseRenameTarget("a view name");
            return new RenameViewStatement(from, to) { Position = position, Doc = doc };
        }
        if (_current.IsKeyword("ENUM"))
        {
            Advance();
            var from = ParseQualifiedNameNode();
            var to = ParseRenameTarget("an enum name");
            return new RenameEnumStatement(from, to) { Position = position, Doc = doc };
        }
        if (_current.IsKeyword("DOMAIN"))
        {
            Advance();
            var from = ParseQualifiedNameNode();
            var to = ParseRenameTarget("a domain name");
            return new RenameDomainStatement(from, to) { Position = position, Doc = doc };
        }
        if (_current.IsKeyword("TYPE"))
        {
            Advance();
            var from = ParseQualifiedNameNode();
            var to = ParseRenameTarget("a type name");
            return new RenameCompositeTypeStatement(from, to) { Position = position, Doc = doc };
        }
        if (_current.IsKeyword("SEQUENCE"))
        {
            Advance();
            var from = ParseQualifiedNameNode();
            var to = ParseRenameTarget("a sequence name");
            return new RenameSequenceStatement(from, to) { Position = position, Doc = doc };
        }
        if (_current.IsKeyword("FUNCTION") || _current.IsKeyword("PROCEDURE") || _current.IsKeyword("ROUTINE"))
        {
            // Functions and procedures share one name space, so all three spellings rename a routine,
            // mirroring DROP.
            Advance();
            var from = ParseQualifiedNameNode();
            var to = ParseRenameTarget("a routine name");
            return new RenameRoutineStatement(from, to) { Position = position, Doc = doc };
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
        ExpectKeyword("SCHEMA");
        var schema = ExpectIdentifierNode("a schema name");
        Expect(TokenKind.Semicolon, "';'");
        return new PartialSchemaStatement(schema) { Position = position, Doc = doc };
    }

    private Identifier ParseRenameTarget(string what)
    {
        ExpectKeyword("TO");
        var to = ExpectIdentifierNode(what);
        Expect(TokenKind.Semicolon, "';'");
        return to;
    }

    /// <summary>
    /// Parses the fully qualified column path (<c>schema.table.column</c>) of a RENAME COLUMN.
    /// </summary>
    private MemberPath ParseColumnPath()
    {
        var schema = ExpectIdentifierNode("a schema name");
        Expect(TokenKind.Dot, "'.' in the column path (schema.table.column)");
        var table = ExpectIdentifierNode("a table name");
        Expect(TokenKind.Dot, "'.' in the column path (schema.table.column)");
        var column = ExpectIdentifierNode("a column name");
        return new MemberPath(schema, table, column) { Position = schema.Position };
    }
}
