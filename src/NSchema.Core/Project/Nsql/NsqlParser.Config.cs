using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Syntax.Config;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql;

internal sealed partial class NsqlParser
{
    /// <summary>
    /// Parses the whole document under the configuration grammar: only configuration statements are legal.
    /// Configuration and project statements never share a file — a file parses as one grammar or the other.
    /// </summary>
    public NsqlConfigDocument ParseConfig() => new(ParseDocumentBody(ParseConfigGrammarStatement));

    private ConfigStatement ParseConfigGrammarStatement(string? doc)
    {
        if (_current.IsKeyword(NsqlKeywords.State))
        {
            return ParseStateConfigStatement(doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Database))
        {
            return ParseDatabaseConfigStatement(doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Plugin))
        {
            return ParsePluginConfigStatement(doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Engine))
        {
            return ParseEngineConfigStatement(doc);
        }
        if (_current.Kind == TokenKind.Identifier)
        {
            throw Error($"Unknown configuration statement '{_current.Text}'; a configuration file holds only PLUGIN, ENGINE, DATABASE, and STATE statements.");
        }
        throw Error($"Unexpected '{_current.Text}'; expected a configuration statement.");
    }

    /// <summary>
    /// Parses <c>DATABASE [label] ( … ) ;</c> — the optional label names the plugin that serves it.
    /// </summary>
    private DatabaseStatement ParseDatabaseConfigStatement(string? doc)
    {
        var (position, label, attributes) = ParseConfigStatementBody();
        return new DatabaseStatement(label, attributes) { Position = position, Doc = doc };
    }

    /// <summary>
    /// Parses <c>STATE [label] ( … ) ;</c> — the optional label names the plugin that serves it.
    /// </summary>
    private StateStatement ParseStateConfigStatement(string? doc)
    {
        var (position, label, attributes) = ParseConfigStatementBody();
        return new StateStatement(label, attributes) { Position = position, Doc = doc };
    }

    /// <summary>
    /// Parses <c>PLUGIN &lt;label&gt; ( … ) ;</c> — the label is required: it is the name the plugin is referenced by.
    /// </summary>
    private PluginStatement ParsePluginConfigStatement(string? doc)
    {
        var (position, label, attributes) = ParseConfigStatementBody();
        if (label is null)
        {
            throw new NsqlSyntaxException("A PLUGIN statement requires a label naming the plugin, e.g. PLUGIN pg ( … ).", position);
        }
        return new PluginStatement(label, attributes) { Position = position, Doc = doc };
    }

    /// <summary>
    /// Parses <c>ENGINE ( … ) ;</c> — no label: there is only one engine.
    /// </summary>
    private EngineStatement ParseEngineConfigStatement(string? doc)
    {
        var (position, label, attributes) = ParseConfigStatementBody();
        if (label is not null)
        {
            throw new NsqlSyntaxException("An ENGINE statement takes no label; there is only one engine.", label.Position);
        }
        return new EngineStatement(attributes) { Position = position, Doc = doc };
    }

    /// <summary>
    /// Parses the shape every configuration statement shares: <c>KEYWORD [label] ( key = value , … ) ;</c>.
    /// Each statement's own rules (whether the label is required, forbidden, or optional) belong to its
    /// <c>Parse*ConfigStatement</c> method, not here.
    /// </summary>
    private (SourcePosition Position, Identifier? Label, List<ConfigAttribute> Attributes) ParseConfigStatementBody()
    {
        var position = _current.Position;
        Advance(); // the statement keyword

        // An optional bare-identifier label, e.g. the 'postgres' in `DATABASE postgres ( … )`.
        Identifier? label = null;
        if (_current.Kind == TokenKind.Identifier)
        {
            var token = Advance();
            label = new Identifier(token.Text) { Position = token.Position };
        }

        Expect(TokenKind.LeftParen, "'(' to begin the configuration attributes");
        var attributes = new List<ConfigAttribute>();
        if (_current.Kind != TokenKind.RightParen)
        {
            do
            {
                var keyPosition = _current.Position;
                var key = ParseConfigKey();
                if (attributes.Any(a => string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new NsqlSyntaxException($"Configuration attribute '{key}' is specified more than once.", keyPosition);
                }
                Expect(TokenKind.Equals, "'=' after a configuration attribute name");
                var value = ParseConfigValueNode();
                attributes.Add(new ConfigAttribute(key, value) { Position = keyPosition });
            }
            while (Match(TokenKind.Comma));
        }
        Expect(TokenKind.RightParen, "')' or ',' after a configuration attribute");
        Expect(TokenKind.Semicolon, "';'");

        return (position, label, attributes);
    }

    /// <summary>
    /// Parses a (possibly dotted) configuration attribute name, e.g. <c>path</c> or <c>pool.max</c>.
    /// </summary>
    private string ParseConfigKey()
    {
        var key = ExpectIdentifierNode("a configuration attribute name").Value;
        while (Match(TokenKind.Dot))
        {
            key += "." + ExpectIdentifierNode("a configuration attribute name segment").Value;
        }
        return key;
    }

    /// <summary>Parses a configuration scalar: string, (signed) integer, <c>true</c>/<c>false</c>, or bare identifier.</summary>
    private ConfigValueNode ParseConfigValueNode()
    {
        var position = _current.Position;
        switch (_current.Kind)
        {
            case TokenKind.String:
                return new StringValue(Advance().Text) { Position = position };
            case TokenKind.Integer:
            case TokenKind.Minus:
                return new IntegerValue(ExpectSignedIntegerValue()) { Position = position };
            case TokenKind.Identifier:
                var text = Advance().Text;
                if (NsqlKeywords.Comparer.Equals(text, "true")) { return new BooleanValue(true) { Position = position }; }
                if (NsqlKeywords.Comparer.Equals(text, "false")) { return new BooleanValue(false) { Position = position }; }
                return new IdentifierValue(text) { Position = position };
            default:
                throw Error("Expected a configuration value (a string, integer, true, false, or identifier).");
        }
    }

    /// <summary>
    /// Reads a boolean value, mirroring the config model's kind check.
    /// </summary>
    private static bool AsBoolean(ConfigValueNode value) => value is BooleanValue b
        ? b.Value
        : throw new InvalidOperationException($"Configuration value of kind {KindName(value)} is not a boolean.");

    private static string KindName(ConfigValueNode value) => value switch
    {
        StringValue => "String",
        IntegerValue => "Integer",
        BooleanValue => "Boolean",
        _ => "Identifier",
    };
}
