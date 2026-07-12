using NSchema.Project.Ddl.Models;
using NSchema.Project.Domain.Models.Tables;

namespace NSchema.Project.Ddl;

internal sealed partial class DdlParser
{
    private void ParseGrant(SchemaAccumulator schemas)
    {
        Advance(); // GRANT

        if (_current.IsKeyword("USAGE"))
        {
            if (_templateSchemaContext is not null)
            {
                throw Error("GRANT USAGE ON SCHEMA is not supported inside a template; declare schema grants alongside the schema.");
            }
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
}
