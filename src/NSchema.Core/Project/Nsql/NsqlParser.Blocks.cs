using System.Globalization;
using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Syntax.Blocks;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql;

internal sealed partial class NsqlParser
{
    /// <summary>The block keyword the current token leads with, or <see langword="null"/> when it is none.</summary>
    private BlockKeyword? CurrentBlockKeyword() =>
        _current.IsKeyword(NsqlKeywords.Plugin) ? BlockKeyword.Plugin
        : _current.IsKeyword(NsqlKeywords.Engine) ? BlockKeyword.Engine
        : _current.IsKeyword(NsqlKeywords.Database) ? BlockKeyword.Database
        : _current.IsKeyword(NsqlKeywords.State) ? BlockKeyword.State
        : _current.IsKeyword(NsqlKeywords.Lock) ? BlockKeyword.Lock
        : null;

    /// <summary>
    /// Parses a block — <c>KEYWORD [label] ( key = value , … ) ;</c> — and enforces the keyword's label rule
    /// (PLUGIN requires one; ENGINE and LOCK forbid one; DATABASE and STATE are optional). Which keywords a
    /// given file admits is the caller's rule (see <see cref="ParseConfigurationBlock"/>, <see cref="ParseLockBlock"/>).
    /// </summary>
    private BlockStatement ParseBlock(BlockKeyword keyword, string? doc)
    {
        var (position, label, attributes) = ParseBlockBody();
        switch (keyword)
        {
            case BlockKeyword.Plugin when label is null:
                throw new NsqlSyntaxException("A PLUGIN statement requires a label naming the plugin, e.g. PLUGIN pg ( … ).", position);
            case BlockKeyword.Engine when label is not null:
                throw new NsqlSyntaxException("An ENGINE statement takes no label; there is only one engine.", label.Position);
            case BlockKeyword.Lock when label is not null:
                throw new NsqlSyntaxException("A LOCK statement takes no label; it identifies its package by the 'source' attribute.", label.Position);
        }
        return new BlockStatement(keyword, label, attributes) { Position = position, Doc = doc };
    }

    /// <summary>
    /// Parses the shape every block shares: <c>KEYWORD [label] ( key = value , … ) ;</c>.
    /// </summary>
    private (SourcePosition Position, Identifier? Label, List<BlockAttribute> Attributes) ParseBlockBody()
    {
        var position = _current.Position;
        Advance(); // the block keyword

        // An optional bare-identifier label, e.g. the 'postgres' in `DATABASE postgres ( … )`.
        Identifier? label = null;
        if (_current.Kind == TokenKind.Identifier)
        {
            var token = Advance();
            label = new Identifier(token.Text) { Position = token.Position };
        }

        Expect(TokenKind.LeftParen, "'(' to begin the block's attributes");
        var attributes = new List<BlockAttribute>();
        if (_current.Kind != TokenKind.RightParen)
        {
            do
            {
                var keyPosition = _current.Position;
                var key = ParseBlockKey();
                if (attributes.Any(a => string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new NsqlSyntaxException($"Attribute '{key}' is specified more than once.", keyPosition);
                }
                Expect(TokenKind.Equals, "'=' after an attribute name");
                var value = ParseBlockValue();
                attributes.Add(new BlockAttribute(key, value) { Position = keyPosition });
            }
            while (Match(TokenKind.Comma));
        }
        Expect(TokenKind.RightParen, "')' or ',' after an attribute");
        Expect(TokenKind.Semicolon, "';'");

        return (position, label, attributes);
    }

    /// <summary>
    /// Parses a (possibly dotted) attribute name, e.g. <c>path</c> or <c>pool.max</c>.
    /// </summary>
    private string ParseBlockKey()
    {
        var key = ExpectIdentifierNode("an attribute name").Value;
        while (Match(TokenKind.Dot))
        {
            key += "." + ExpectIdentifierNode("an attribute name segment").Value;
        }
        return key;
    }

    /// <summary>
    /// Parses a scalar — a string, (signed) integer, <c>true</c>/<c>false</c>, or bare identifier — as the text
    /// it was written as. The binder converts it to the attribute's target type.
    /// </summary>
    private string ParseBlockValue()
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
                throw Error("Expected a value (a string, integer, true, false, or identifier).");
        }
    }

    // Reads a value written as true/false; a parse-time check for the few options (e.g. run_outside_transaction)
    // the parser resolves directly rather than through the binder.
    private static bool AsBoolean(string value, SourcePosition position) => bool.TryParse(value, out var result)
        ? result
        : throw new NsqlSyntaxException($"Expected 'true' or 'false', but got '{value}'.", position);
}
