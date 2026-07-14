using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Syntax.Schemas;
using NSchema.Project.Nsql.Syntax.Tables;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql;

internal sealed partial class NsqlParser
{
    private NsqlStatement ParseGrant(string? doc)
    {
        var position = _current.Position;
        Advance(); // GRANT

        if (_current.IsKeyword("USAGE"))
        {
            if (_inTemplateBody)
            {
                throw Error("GRANT USAGE ON SCHEMA is not supported inside a template; declare schema grants alongside the schema.");
            }
            Advance();
            ExpectKeyword("ON");
            ExpectKeyword("SCHEMA");
            var schema = ExpectIdentifierNode("a schema name");
            ExpectKeyword("TO");
            var role = ExpectIdentifierNode("a role name");
            Expect(TokenKind.Semicolon, "';'");
            return new GrantSchemaUsageStatement(schema, role) { Position = position, Doc = doc };
        }

        var privileges = ParseTablePrivileges();
        ExpectKeyword("ON");
        var on = ParseQualifiedNameNode();
        ExpectKeyword("TO");
        var grantee = ExpectIdentifierNode("a role name");
        Expect(TokenKind.Semicolon, "';'");
        return new GrantTableStatement(privileges, on, grantee) { Position = position, Doc = doc };
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
}
