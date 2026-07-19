using NSchema.Model;
using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Syntax.Schemas;
using NSchema.Project.Nsql.Syntax.Tables;
using NSchema.Project.Nsql.Syntax.Templates;
using NSchema.Project.Nsql.Tokens;

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
        ExpectKeyword(NsqlKeywords.Begin);

        return forTable
            ? ParseTableTemplateBody(position, name, doc)
            : ParseSchemaTemplateBody(position, name, namePosition, doc);
    }

    /// <summary>
    /// Parses the optional kind marker after the template name: <c>FOR SCHEMA</c> (the default) or <c>FOR TABLE</c>.
    /// </summary>
    private bool ParseTemplateKindIsTable()
    {
        if (!_current.IsKeyword(NsqlKeywords.For))
        {
            return false;
        }
        Advance(); // FOR

        if (_current.IsKeyword(NsqlKeywords.Schema))
        {
            Advance();
            return false;
        }
        if (_current.IsKeyword(NsqlKeywords.Table))
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
            while (!_current.IsKeyword(NsqlKeywords.End))
            {
                if (_current.Kind == TokenKind.EndOfFile)
                {
                    throw new NsqlSyntaxException($"Unterminated template '{name.Value}'; expected END.", namePosition);
                }

                var statementDoc = TakePendingDoc();
                if (_current.IsKeyword(NsqlKeywords.End))
                {
                    break;
                }
                try
                {
                    statements.Add(ParseTemplateStatement(statementDoc));
                }
                catch (NsqlSyntaxException error)
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
            if (!_current.IsKeyword(NsqlKeywords.End))
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
        ExpectKeyword(NsqlKeywords.End);
        Expect(TokenKind.Semicolon, "';'");

        return new TableTemplateStatement(name, members) { Position = position, Doc = doc };
    }

    /// <summary>
    /// Skips to just past the next <c>;</c>, stopping short of <c>END</c> so the template's terminator
    /// still closes the body.
    /// </summary>
    private void ResyncInTemplateBody()
    {
        while (_current.Kind != TokenKind.EndOfFile && !_current.IsKeyword(NsqlKeywords.End))
        {
            if (Advance().Kind == TokenKind.Semicolon)
            {
                return;
            }
        }
    }

    private NsqlStatement ParseTemplateStatement(string? doc)
    {
        if (_current.IsKeyword(NsqlKeywords.Create))
        {
            return ParseCreate(doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Grant))
        {
            return ParseGrant(doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Script))
        {
            return ParseScript(doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Rename))
        {
            return RequireObjectDirective(ParseRename(doc));
        }
        throw Error($"Unexpected '{_current.Text}' inside a template; expected a CREATE, GRANT, SCRIPT, or RENAME statement, or END.");
    }

    /// <summary>
    /// Rejects the directives whose subject a template can't own: a template describes the objects within a
    /// schema, so a schema-level directive — or a view directive, views being unavailable in a template — has
    /// no place in its body.
    /// </summary>
    private NsqlStatement RequireObjectDirective(NsqlStatement directive)
    {
        var rejected = directive switch
        {
            RenameSchemaStatement => "RENAME SCHEMA",
            RenameObjectStatement { Kind: ObjectKind.View } => "RENAME VIEW",
            _ => null,
        };
        if (rejected is not null)
        {
            throw Error($"A {rejected} directive is not allowed in a template body.");
        }
        return directive;
    }

    /// <summary>
    /// Parses a template application: <c>APPLY TEMPLATE name IN SCHEMA a[, b …];</c>.
    /// </summary>
    private ApplyTemplateStatement ParseApplyTemplate(string? doc)
    {
        var position = _current.Position;
        Advance(); // APPLY
        ExpectKeyword(NsqlKeywords.Template);
        var name = ExpectIdentifierNode("a template name");
        ExpectKeyword(NsqlKeywords.In);
        ExpectKeyword(NsqlKeywords.Schema);

        var schemaNames = new List<Identifier>();
        do
        {
            var schemaPosition = _current.Position;
            var schemaName = ExpectIdentifierNode("a schema name");
            if (schemaNames.Any(s => string.Equals(s.Value, schemaName.Value, StringComparison.Ordinal)))
            {
                throw new NsqlSyntaxException($"Schema '{schemaName.Value}' is listed more than once.", schemaPosition);
            }
            schemaNames.Add(schemaName);
        }
        while (Match(TokenKind.Comma));
        Expect(TokenKind.Semicolon, "';'");

        return new ApplyTemplateStatement(name, schemaNames) { Position = position, Doc = doc };
    }
}
