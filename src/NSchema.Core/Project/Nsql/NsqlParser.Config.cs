using System.Globalization;
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
        if (CurrentConfigKeyword() is not { } keyword || keyword == ConfigKeyword.Lock)
        {
            throw _current.Kind == TokenKind.Identifier
                ? Error($"Unknown configuration statement '{_current.Text}'; a configuration file holds only PLUGIN, ENGINE, DATABASE, and STATE statements.")
                : Error($"Unexpected '{_current.Text}'; expected a configuration statement.");
        }
        return ParseKeywordStatement(keyword, doc);
    }

    /// <summary>The configuration keyword the current token leads with, or <see langword="null"/> when it is none.</summary>
    private ConfigKeyword? CurrentConfigKeyword() =>
        _current.IsKeyword(NsqlKeywords.Plugin) ? ConfigKeyword.Plugin
        : _current.IsKeyword(NsqlKeywords.Engine) ? ConfigKeyword.Engine
        : _current.IsKeyword(NsqlKeywords.Database) ? ConfigKeyword.Database
        : _current.IsKeyword(NsqlKeywords.State) ? ConfigKeyword.State
        : _current.IsKeyword(NsqlKeywords.Lock) ? ConfigKeyword.Lock
        : null;

    /// <summary>
    /// Parses the body shared by every keyword — <c>[label] ( key = value , … ) ;</c> — and enforces the
    /// keyword's label rule (PLUGIN requires one; ENGINE and LOCK forbid one; DATABASE and STATE are optional).
    /// </summary>
    private ConfigStatement ParseKeywordStatement(ConfigKeyword keyword, string? doc)
    {
        var (position, label, attributes) = ParseConfigStatementBody();
        switch (keyword)
        {
            case ConfigKeyword.Plugin when label is null:
                throw new NsqlSyntaxException("A PLUGIN statement requires a label naming the plugin, e.g. PLUGIN pg ( … ).", position);
            case ConfigKeyword.Engine when label is not null:
                throw new NsqlSyntaxException("An ENGINE statement takes no label; there is only one engine.", label.Position);
            case ConfigKeyword.Lock when label is not null:
                throw new NsqlSyntaxException("A LOCK statement takes no label; it identifies its package by the 'source' attribute.", label.Position);
        }
        return new ConfigStatement(keyword, label, attributes) { Position = position, Doc = doc };
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
                var value = ParseConfigValue();
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

    /// <summary>
    /// Parses a configuration scalar — a string, (signed) integer, <c>true</c>/<c>false</c>, or bare identifier —
    /// as the text it was written as. The configuration binder converts it to the attribute's target type.
    /// </summary>
    private string ParseConfigValue()
    {
        switch (_current.Kind)
        {
            case TokenKind.String:
            case TokenKind.Identifier:
                return Advance().Text;
            case TokenKind.Integer:
            case TokenKind.Minus:
                return ExpectSignedIntegerValue().ToString(CultureInfo.InvariantCulture);
            default:
                throw Error("Expected a configuration value (a string, integer, true, false, or identifier).");
        }
    }

    // Reads a value written as true/false; a parse-time check for the few options (e.g. run_outside_transaction)
    // the parser resolves directly rather than through the configuration binder.
    private static bool AsBoolean(string value, SourcePosition position) => bool.TryParse(value, out var result)
        ? result
        : throw new NsqlSyntaxException($"Expected 'true' or 'false', but got '{value}'.", position);
}
