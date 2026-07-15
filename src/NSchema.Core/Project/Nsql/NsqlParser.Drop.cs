using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Syntax.CompositeTypes;
using NSchema.Project.Nsql.Syntax.Domains;
using NSchema.Project.Nsql.Syntax.Enums;
using NSchema.Project.Nsql.Syntax.Extensions;
using NSchema.Project.Nsql.Syntax.Routines;
using NSchema.Project.Nsql.Syntax.Schemas;
using NSchema.Project.Nsql.Syntax.Sequences;
using NSchema.Project.Nsql.Syntax.Tables;
using NSchema.Project.Nsql.Syntax.Views;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql;

internal sealed partial class NsqlParser
{
    private NsqlStatement ParseDrop(string? doc)
    {
        var position = _current.Position;
        Advance(); // DROP

        if (_current.IsKeyword(NsqlKeywords.Schema))
        {
            Advance();
            var name = ExpectIdentifierNode("a schema name");
            Expect(TokenKind.Semicolon, "';'");
            return new DropSchemaStatement(name) { Position = position, Doc = doc };
        }
        if (_current.IsKeyword(NsqlKeywords.Table))
        {
            Advance();
            var name = ParseQualifiedNameNode();
            Expect(TokenKind.Semicolon, "';'");
            return new DropTableStatement(name) { Position = position, Doc = doc };
        }
        if (_current.IsKeyword(NsqlKeywords.View) || _current.IsKeyword(NsqlKeywords.Materialized))
        {
            // DROP VIEW and DROP MATERIALIZED VIEW both record a dropped view (the kind is resolved from the
            // current state when the drop is planned).
            if (Advance().IsKeyword(NsqlKeywords.Materialized))
            {
                ExpectKeyword(NsqlKeywords.View);
            }
            var name = ParseQualifiedNameNode();
            Expect(TokenKind.Semicolon, "';'");
            return new DropViewStatement(name) { Position = position, Doc = doc };
        }
        if (_current.IsKeyword(NsqlKeywords.Enum))
        {
            Advance();
            var name = ParseQualifiedNameNode();
            Expect(TokenKind.Semicolon, "';'");
            return new DropEnumStatement(name) { Position = position, Doc = doc };
        }
        if (_current.IsKeyword(NsqlKeywords.Domain))
        {
            Advance();
            var name = ParseQualifiedNameNode();
            Expect(TokenKind.Semicolon, "';'");
            return new DropDomainStatement(name) { Position = position, Doc = doc };
        }
        if (_current.IsKeyword(NsqlKeywords.Type))
        {
            Advance();
            var name = ParseQualifiedNameNode();
            Expect(TokenKind.Semicolon, "';'");
            return new DropCompositeTypeStatement(name) { Position = position, Doc = doc };
        }
        if (_current.IsKeyword(NsqlKeywords.Sequence))
        {
            Advance();
            var name = ParseQualifiedNameNode();
            Expect(TokenKind.Semicolon, "';'");
            return new DropSequenceStatement(name) { Position = position, Doc = doc };
        }
        if (_current.IsKeyword(NsqlKeywords.Function) || _current.IsKeyword(NsqlKeywords.Procedure) || _current.IsKeyword(NsqlKeywords.Routine))
        {
            // DROP FUNCTION / DROP PROCEDURE / DROP ROUTINE all record a dropped routine (the kind is resolved
            // from the current state when the drop is planned), since they share one name space.
            Advance();
            var name = ParseQualifiedNameNode();
            Expect(TokenKind.Semicolon, "';'");
            return new DropRoutineStatement(name) { Position = position, Doc = doc };
        }
        if (_current.IsKeyword(NsqlKeywords.Extension))
        {
            Advance();
            var name = ParseExtensionNameNode();
            Expect(TokenKind.Semicolon, "';'");
            return new DropExtensionStatement(name) { Position = position, Doc = doc };
        }
        throw Error($"Expected SCHEMA, TABLE, VIEW, ENUM, DOMAIN, TYPE, SEQUENCE, FUNCTION, PROCEDURE, ROUTINE or EXTENSION after DROP, found '{_current.Text}'.");
    }
}
