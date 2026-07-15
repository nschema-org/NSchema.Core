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

        if (_current.IsKeyword(NsqlKeywords.Usage))
        {
            if (_inTemplateBody)
            {
                throw Error("GRANT USAGE ON SCHEMA is not supported inside a template; declare schema grants alongside the schema.");
            }
            Advance();
            ExpectKeyword(NsqlKeywords.On);
            ExpectKeyword(NsqlKeywords.Schema);
            var schema = ExpectIdentifierNode("a schema name");
            ExpectKeyword(NsqlKeywords.To);
            var role = ExpectIdentifierNode("a role name");
            Expect(TokenKind.Semicolon, "';'");
            return new GrantSchemaUsageStatement(schema, role) { Position = position, Doc = doc };
        }

        var privileges = ParseTablePrivileges();
        ExpectKeyword(NsqlKeywords.On);
        var on = ParseQualifiedNameNode();
        ExpectKeyword(NsqlKeywords.To);
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
        if (_current.IsKeyword(NsqlKeywords.Select)) { Advance(); return TablePrivilege.Select; }
        if (_current.IsKeyword(NsqlKeywords.Insert)) { Advance(); return TablePrivilege.Insert; }
        if (_current.IsKeyword(NsqlKeywords.Update)) { Advance(); return TablePrivilege.Update; }
        if (_current.IsKeyword(NsqlKeywords.Delete)) { Advance(); return TablePrivilege.Delete; }
        throw Error($"Expected a privilege (SELECT, INSERT, UPDATE, DELETE), found '{_current.Text}'.");
    }
}
