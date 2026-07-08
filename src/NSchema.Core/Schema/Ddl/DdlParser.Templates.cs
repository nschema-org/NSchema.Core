using NSchema.Schema.Ddl.Model;
using NSchema.Schema.Model.Schemas;
using NSchema.Schema.Model.Tables;
using NSchema.Schema.Model.Templates;

namespace NSchema.Schema.Ddl;

internal sealed partial class DdlParser
{
    /// <summary>
    /// Parses a template definition: <c>TEMPLATE name [FOR SCHEMA|TABLE] BEGIN … END;</c>. A schema template's
    /// body holds statements; a table template's body holds comma-separated table members.
    /// </summary>
    private TemplateDefinition ParseTemplate()
    {
        Advance(); // TEMPLATE
        var namePosition = _current.Position;
        var name = ExpectIdentifier("a template name");
        var kind = ParseTemplateKind();
        ExpectKeyword("BEGIN");

        return kind == TemplateKind.Table
            ? ParseTableTemplateBody(name)
            : ParseSchemaTemplateBody(name, namePosition);
    }

    /// <summary>
    /// Parses the optional kind marker after the template name: <c>FOR SCHEMA</c> (the default) or <c>FOR TABLE</c>.
    /// </summary>
    private TemplateKind ParseTemplateKind()
    {
        if (!_current.IsKeyword("FOR"))
        {
            return TemplateKind.Schema;
        }
        Advance(); // FOR

        if (_current.IsKeyword("SCHEMA"))
        {
            Advance();
            return TemplateKind.Schema;
        }
        if (_current.IsKeyword("TABLE"))
        {
            Advance();
            return TemplateKind.Table;
        }
        throw Error($"Expected SCHEMA or TABLE after FOR, found '{_current.Text}'.");
    }

    private TemplateDefinition ParseSchemaTemplateBody(string name, SourcePosition namePosition)
    {
        var body = new SchemaAccumulator();
        // INCLUDEs written in this body belong to the definition (re-targeted per instance at expansion), not
        // to the document.
        var bodyIncludes = new List<TemplateInclude>();
        _templateSchemaContext = TemplateDefinition.TargetSchemaPlaceholder;
        try
        {
            while (!_current.IsKeyword("END"))
            {
                if (_current.Kind == TokenKind.EndOfFile)
                {
                    throw new DdlSyntaxException($"Unterminated template '{name}'; expected END.", namePosition);
                }

                var doc = TakePendingDoc();
                if (_current.IsKeyword("END"))
                {
                    break;
                }
                ParseTemplateStatement(body, bodyIncludes, doc);
            }
        }
        finally
        {
            _templateSchemaContext = null;
        }
        Advance(); // END
        Expect(TokenKind.Semicolon, "';'");

        // Every declaration must have bound to the placeholder — a qualified CREATE inside the body would create
        // the same object once per application, so it is rejected here (qualified *references* are fine and never
        // reach the accumulator as a schema entry).
        var fragment = body.Build();
        var stray = fragment.Schemas.FirstOrDefault(s => s.Name != TemplateDefinition.TargetSchemaPlaceholder);
        if (stray is not null)
        {
            throw new DdlSyntaxException(
                $"Template '{name}' declares objects in schema '{stray.Name}'; objects inside a template must use " +
                "unqualified names so they are created in each schema the template is applied to.", namePosition);
        }

        var objects = fragment.Schemas.FirstOrDefault() ?? new SchemaDefinition(TemplateDefinition.TargetSchemaPlaceholder);
        return new TemplateDefinition(name, TemplateKind.Schema, objects) { Includes = bodyIncludes };
    }

    /// <summary>
    /// Parses a table template's body — comma-separated table members, the same grammar as a table body — carried
    /// as a single placeholder-named table.
    /// </summary>
    private TemplateDefinition ParseTableTemplateBody(string name)
    {
        var body = new TableBody();
        _templateSchemaContext = TemplateDefinition.TargetSchemaPlaceholder;
        _inTableTemplateBody = true;
        try
        {
            if (!_current.IsKeyword("END"))
            {
                do
                {
                    var itemDoc = TakePendingDoc();
                    ParseTableItem(itemDoc, body);
                }
                while (Match(TokenKind.Comma));
            }
        }
        finally
        {
            _templateSchemaContext = null;
            _inTableTemplateBody = false;
        }
        ExpectKeyword("END");
        Expect(TokenKind.Semicolon, "';'");

        var members = new Table(TemplateDefinition.TargetSchemaPlaceholder, null, body.PrimaryKey, null,
            body.Columns, body.ForeignKeys, body.UniqueConstraints, body.CheckConstraints, body.ExclusionConstraints, body.Indexes);
        var objects = new SchemaDefinition(TemplateDefinition.TargetSchemaPlaceholder, Tables: [members]);
        return new TemplateDefinition(name, TemplateKind.Table, objects);
    }

    private void ParseTemplateStatement(SchemaAccumulator schemas, List<TemplateInclude> includes, string? doc)
    {
        if (_current.IsKeyword("CREATE"))
        {
            ParseCreate(schemas, includes, doc);
        }
        else if (_current.IsKeyword("GRANT"))
        {
            ParseGrant(schemas);
        }
        else
        {
            throw Error($"Unexpected '{_current.Text}' inside a template; expected a CREATE or GRANT statement, or END.");
        }
    }

    /// <summary>
    /// Parses a template application: <c>APPLY TEMPLATE name IN SCHEMA a[, b …];</c>.
    /// </summary>
    private TemplateApplication ParseApplyTemplate()
    {
        Advance(); // APPLY
        ExpectKeyword("TEMPLATE");
        var name = ExpectIdentifier("a template name");
        ExpectKeyword("IN");
        ExpectKeyword("SCHEMA");

        var schemaNames = new List<string>();
        do
        {
            var position = _current.Position;
            var schemaName = ExpectIdentifier("a schema name");
            if (schemaNames.Contains(schemaName, StringComparer.OrdinalIgnoreCase))
            {
                throw new DdlSyntaxException($"Schema '{schemaName}' is listed more than once.", position);
            }
            schemaNames.Add(schemaName);
        }
        while (Match(TokenKind.Comma));
        Expect(TokenKind.Semicolon, "';'");

        return new TemplateApplication(name, schemaNames);
    }
}
