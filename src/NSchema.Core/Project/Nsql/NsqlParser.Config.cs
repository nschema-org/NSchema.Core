using NSchema.Project.Ddl;
using NSchema.Project.Ddl.Models;
using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Syntax.Config;

namespace NSchema.Project.Nsql;

internal sealed partial class NsqlParser
{
    /// <summary>
    /// Parses a configuration statement: <c>BACKEND|PROVIDER [label] ( key = value , … ) ;</c>.
    /// The optional label is a bare identifier; attributes are a flat, comma-separated list.
    /// </summary>
    private ConfigStatement ParseConfigStatement(string? doc, bool backend)
    {
        var position = _current.Position;
        Advance(); // BACKEND | PROVIDER

        // An optional bare-identifier label, e.g. the 'postgres' in `PROVIDER postgres ( … )`.
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
                    throw new DdlSyntaxException($"Configuration attribute '{key}' is specified more than once.", keyPosition);
                }
                Expect(TokenKind.Equals, "'=' after a configuration attribute name");
                var value = ParseConfigValueNode();
                attributes.Add(new ConfigAttribute(key, value) { Position = keyPosition });
            }
            while (Match(TokenKind.Comma));
        }
        Expect(TokenKind.RightParen, "')' or ',' after a configuration attribute");
        Expect(TokenKind.Semicolon, "';'");

        return backend
            ? new BackendStatement(label, attributes) { Position = position, Doc = doc }
            : new ProviderStatement(label, attributes) { Position = position, Doc = doc };
    }

    /// <summary>
    /// Parses a (possibly dotted) configuration attribute name, e.g. <c>path</c> or <c>pool.max</c>.
    /// </summary>
    private string ParseConfigKey()
    {
        var key = ExpectIdentifierNode("a configuration attribute name").Text;
        while (Match(TokenKind.Dot))
        {
            key += "." + ExpectIdentifierNode("a configuration attribute name segment").Text;
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
