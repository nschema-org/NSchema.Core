using NSchema.Project.Ddl;
using NSchema.Project.Ddl.Models;
using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Syntax.Tables;
using NSchema.Project.Nsql.Syntax.Templates;

namespace NSchema.Project.Nsql;

internal sealed partial class NsqlParser
{
    /// <summary>
    /// Parses a template definition.
    /// </summary>
    private NsqlStatement ParseTemplate(string? doc)
    {
        var position = _current.Position;
        Advance(); // TEMPLATE
        var namePosition = _current.Position;
        var name = ExpectIdentifierNode("a template name");
        var forTable = ParseTemplateKindIsTable();
        ExpectKeyword("BEGIN");

        return forTable
            ? ParseTableTemplateBody(position, name, doc)
            : ParseSchemaTemplateBody(position, name, namePosition, doc);
    }

    /// <summary>
    /// Parses the optional kind marker after the template name: <c>FOR SCHEMA</c> (the default) or <c>FOR TABLE</c>.
    /// </summary>
    private bool ParseTemplateKindIsTable()
    {
        if (!_current.IsKeyword("FOR"))
        {
            return false;
        }
        Advance(); // FOR

        if (_current.IsKeyword("SCHEMA"))
        {
            Advance();
            return false;
        }
        if (_current.IsKeyword("TABLE"))
        {
            Advance();
            return true;
        }
        throw Error($"Expected SCHEMA or TABLE after FOR, found '{_current.Text}'.");
    }

    private SchemaTemplateStatement ParseSchemaTemplateBody(SourcePosition position, Identifier name, SourcePosition namePosition, string? doc)
    {
        var statements = new List<NsqlStatement>();
        _inTemplateBody = true;
        try
        {
            while (!_current.IsKeyword("END"))
            {
                if (_current.Kind == TokenKind.EndOfFile)
                {
                    throw new DdlSyntaxException($"Unterminated template '{name.Text}'; expected END.", namePosition);
                }

                var statementDoc = TakePendingDoc();
                if (_current.IsKeyword("END"))
                {
                    break;
                }
                try
                {
                    statements.Add(ParseTemplateStatement(statementDoc));
                }
                catch (DdlSyntaxException error)
                {
                    _errors.Add(error);
                    ResyncInTemplateBody();
                }
            }
        }
        finally
        {
            _inTemplateBody = false;
        }
        Advance(); // END
        Expect(TokenKind.Semicolon, "';'");

        return new SchemaTemplateStatement(name, statements) { Position = position, Doc = doc };
    }

    /// <summary>
    /// Parses a table template's body — comma-separated table members, the same grammar as a table body.
    /// </summary>
    private TableTemplateStatement ParseTableTemplateBody(SourcePosition position, Identifier name, string? doc)
    {
        var members = new List<TableMember>();
        _inTemplateBody = true;
        _inTableTemplateBody = true;
        try
        {
            if (!_current.IsKeyword("END"))
            {
                var primaryKeySeen = false;
                do
                {
                    var itemDoc = TakePendingDoc();
                    members.Add(ParseTableItem(itemDoc, ref primaryKeySeen));
                }
                while (Match(TokenKind.Comma));
            }
        }
        finally
        {
            _inTemplateBody = false;
            _inTableTemplateBody = false;
        }
        ExpectKeyword("END");
        Expect(TokenKind.Semicolon, "';'");

        return new TableTemplateStatement(name, members) { Position = position, Doc = doc };
    }

    /// <summary>
    /// Skips to just past the next <c>;</c>, stopping short of <c>END</c> so the template's terminator
    /// still closes the body.
    /// </summary>
    private void ResyncInTemplateBody()
    {
        while (_current.Kind != TokenKind.EndOfFile && !_current.IsKeyword("END"))
        {
            if (Advance().Kind == TokenKind.Semicolon)
            {
                return;
            }
        }
    }

    private NsqlStatement ParseTemplateStatement(string? doc)
    {
        if (_current.IsKeyword("CREATE"))
        {
            return ParseCreate(doc);
        }
        if (_current.IsKeyword("GRANT"))
        {
            return ParseGrant(doc);
        }
        if (_current.IsKeyword("SCRIPT"))
        {
            return ParseScript(doc);
        }
        throw Error($"Unexpected '{_current.Text}' inside a template; expected a CREATE, GRANT, or SCRIPT statement, or END.");
    }

    /// <summary>
    /// Parses a template application: <c>APPLY TEMPLATE name IN SCHEMA a[, b …];</c>.
    /// </summary>
    private ApplyTemplateStatement ParseApplyTemplate(string? doc)
    {
        var position = _current.Position;
        Advance(); // APPLY
        ExpectKeyword("TEMPLATE");
        var name = ExpectIdentifierNode("a template name");
        ExpectKeyword("IN");
        ExpectKeyword("SCHEMA");

        var schemaNames = new List<Identifier>();
        do
        {
            var schemaPosition = _current.Position;
            var schemaName = ExpectIdentifierNode("a schema name");
            if (schemaNames.Any(s => string.Equals(s.Text, schemaName.Text, StringComparison.OrdinalIgnoreCase)))
            {
                throw new DdlSyntaxException($"Schema '{schemaName.Text}' is listed more than once.", schemaPosition);
            }
            schemaNames.Add(schemaName);
        }
        while (Match(TokenKind.Comma));
        Expect(TokenKind.Semicolon, "';'");

        return new ApplyTemplateStatement(name, schemaNames) { Position = position, Doc = doc };
    }
}
