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
    public NsqlConfigDocument ParseConfig()
    {
        var statements = new List<ConfigStatement>();
        string? pendingDoc = null;

        while (_current.Kind != TokenKind.EndOfFile)
        {
            if (_current.Kind == TokenKind.DocComment)
            {
                pendingDoc = _current.Text;
                Advance();
                continue;
            }

            try
            {
                statements.Add(ParseConfigGrammarStatement(pendingDoc));
            }
            catch (NsqlSyntaxException error)
            {
                _errors.Add(error);
                Resync();
            }
            pendingDoc = null;
        }

        return new NsqlConfigDocument(statements);
    }

    private ConfigStatement ParseConfigGrammarStatement(string? doc)
    {
        if (_current.IsKeyword(NsqlKeywords.State))
        {
            return ParseConfigStatement(doc, state: true);
        }
        if (_current.IsKeyword(NsqlKeywords.Database))
        {
            return ParseConfigStatement(doc, state: false);
        }
        if (_current.Kind == TokenKind.Identifier)
        {
            throw Error($"Unknown configuration statement '{_current.Text}'; a configuration file holds only DATABASE and STATE statements.");
        }
        throw Error($"Unexpected '{_current.Text}'; expected a configuration statement.");
    }

    /// <summary>
    /// Parses a configuration statement: <c>DATABASE|STATE [label] ( key = value , … ) ;</c>.
    /// The optional label is a bare identifier; attributes are a flat, comma-separated list.
    /// </summary>
    private ConfigStatement ParseConfigStatement(string? doc, bool state)
    {
        var position = _current.Position;
        Advance(); // DATABASE | STATE

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

        return state
            ? new StateStatement(label, attributes) { Position = position, Doc = doc }
            : new DatabaseStatement(label, attributes) { Position = position, Doc = doc };
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
                if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase)) { return new BooleanValue(true) { Position = position }; }
                if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase)) { return new BooleanValue(false) { Position = position }; }
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
