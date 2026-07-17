using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Syntax.Extensions;
using NSchema.Project.Nsql.Syntax.Schemas;
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
        if (_current.IsKeyword(NsqlKeywords.Extension))
        {
            Advance();
            var name = ParseExtensionNameNode();
            Expect(TokenKind.Semicolon, "';'");
            return new DropExtensionStatement(name) { Position = position, Doc = doc };
        }
        if (TryParseObjectKind() is { } kind)
        {
            var name = ParseQualifiedNameNode();
            Expect(TokenKind.Semicolon, "';'");
            return new DropObjectStatement(kind, name) { Position = position, Doc = doc };
        }
        throw Error($"Expected SCHEMA, TABLE, VIEW, ENUM, DOMAIN, TYPE, SEQUENCE, FUNCTION, PROCEDURE, ROUTINE or EXTENSION after DROP, found '{_current.Text}'.");
    }
}
