using NSchema.Schema.Ddl.Model;

namespace NSchema.Schema.Ddl;

internal sealed partial class DdlParser
{
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
        else if (_current.IsKeyword("DOMAIN"))
        {
            Advance();
            var (schema, domain) = ParseQualifiedName();
            Expect(TokenKind.Semicolon, "';'");
            schemas.DropDomain(schema, domain);
        }
        else if (_current.IsKeyword("TYPE"))
        {
            Advance();
            var (schema, type) = ParseQualifiedName();
            Expect(TokenKind.Semicolon, "';'");
            schemas.DropCompositeType(schema, type);
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
            throw Error($"Expected SCHEMA, TABLE, VIEW, ENUM, DOMAIN, TYPE, SEQUENCE, FUNCTION, PROCEDURE, ROUTINE or EXTENSION after DROP, found '{_current.Text}'.");
        }
    }
}
