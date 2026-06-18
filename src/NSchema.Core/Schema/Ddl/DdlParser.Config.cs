using NSchema.Configuration;
using NSchema.Schema.Ddl.Model;

namespace NSchema.Schema.Ddl;

internal sealed partial class DdlParser
{
    /// <summary>
    /// Parses a configuration block: <c>keyword [label] ( key = value , … ) ;</c>. The keyword and optional label
    /// are bare identifiers; attributes are a flat, comma-separated list.
    /// </summary>
    private ConfigBlock ParseConfigBlock()
    {
        var type = ExpectIdentifier("a configuration block keyword").ToLowerInvariant();

        // An optional bare-identifier label, e.g. the 'postgres' in `PROVIDER postgres ( … )`. None for `NSCHEMA`.
        string? label = null;
        if (_current.Kind == TokenKind.Identifier)
        {
            label = Advance().Text;
        }

        Expect(TokenKind.LeftParen, "'(' to begin the configuration attributes");
        var attributes = new Dictionary<string, ConfigValue>(StringComparer.OrdinalIgnoreCase);
        if (_current.Kind != TokenKind.RightParen)
        {
            do
            {
                var keyPosition = _current.Position;
                var key = ParseConfigKey();
                Expect(TokenKind.Equals, "'=' after a configuration attribute name");
                var value = ParseConfigValue();
                if (!attributes.TryAdd(key, value))
                {
                    throw new DdlSyntaxException($"Configuration attribute '{key}' is specified more than once.", keyPosition);
                }
            }
            while (Match(TokenKind.Comma));
        }
        Expect(TokenKind.RightParen, "')' or ',' after a configuration attribute");
        Expect(TokenKind.Semicolon, "';'");

        return new ConfigBlock(type, label, attributes);
    }

    /// <summary>
    /// Parses a (possibly dotted) configuration attribute name, e.g. <c>path</c> or <c>pool.max</c>.
    /// </summary>
    private string ParseConfigKey()
    {
        var key = ExpectIdentifier("a configuration attribute name");
        while (Match(TokenKind.Dot))
        {
            key += "." + ExpectIdentifier("a configuration attribute name segment");
        }
        return key;
    }

    /// <summary>Parses a configuration scalar: string, (signed) integer, <c>true</c>/<c>false</c>, or bare identifier.</summary>
    private ConfigValue ParseConfigValue()
    {
        switch (_current.Kind)
        {
            case TokenKind.String:
                return ConfigValue.OfString(Advance().Text);
            case TokenKind.Integer:
            case TokenKind.Minus:
                return ConfigValue.OfInteger(ExpectSignedIntegerValue());
            case TokenKind.Identifier:
                var text = Advance().Text;
                if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase)) { return ConfigValue.OfBoolean(true); }
                if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase)) { return ConfigValue.OfBoolean(false); }
                return ConfigValue.OfIdentifier(text);
            default:
                throw Error("Expected a configuration value (a string, integer, true, false, or identifier).");
        }
    }
}
