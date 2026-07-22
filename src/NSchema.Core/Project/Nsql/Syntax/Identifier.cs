using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax;

/// <summary>
/// A name as written in the source, casing preserved.
/// </summary>
/// <param name="Token">The identifier token (bare or quoted).</param>
public sealed record Identifier(Token Token) : NsqlNode
{
    /// <summary>
    /// The name's decoded text — quotes and escapes resolved.
    /// </summary>
    public string Value => Token.Text;

    internal override IEnumerable<NsqlChild> Children => [Token];

    /// <summary>
    /// Builds a synthetic identifier (no source, no trivia) carrying <paramref name="value"/>.
    /// </summary>
    public static Identifier Synthetic(string value) =>
        new(new Token(TokenKind.Identifier, value, SourcePosition.None)) { Position = SourcePosition.None };

    /// <inheritdoc/>
    public override string ToString() => Value;
}
