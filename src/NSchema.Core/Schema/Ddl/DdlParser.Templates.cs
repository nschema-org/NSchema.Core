using NSchema.Schema.Ddl.Model;
using NSchema.Schema.Model.Schemas;
using NSchema.Schema.Model.Templates;

namespace NSchema.Schema.Ddl;

internal sealed partial class DdlParser
{
    /// <summary>
    /// Parses a template definition: <c>TEMPLATE name BEGIN &lt;statements&gt; END;</c>.
    /// </summary>
    private TemplateDefinition ParseTemplate()
    {
        Advance(); // TEMPLATE
        var namePosition = _current.Position;
        var name = ExpectIdentifier("a template name");
        ExpectKeyword("BEGIN");

        var body = new SchemaAccumulator();
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
                ParseTemplateStatement(body, doc);
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
        return new TemplateDefinition(name, objects);
    }

    private void ParseTemplateStatement(SchemaAccumulator schemas, string? doc)
    {
        if (_current.IsKeyword("CREATE"))
        {
            ParseCreate(schemas, doc);
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
