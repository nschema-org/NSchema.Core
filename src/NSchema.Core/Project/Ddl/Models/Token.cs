using NSchema.Project.Nsql;

namespace NSchema.Project.Ddl.Models;

/// <summary>
/// A single lexical token from a DDL source document.
/// </summary>
/// <param name="Kind">The kind of token.</param>
/// <param name="Text">
/// The token's textual payload: the raw identifier for <see cref="TokenKind.Identifier"/>, the unescaped value
/// for <see cref="TokenKind.String"/>, the digits for <see cref="TokenKind.Integer"/>, the trimmed comment body
/// for <see cref="TokenKind.DocComment"/>, the literal punctuation for the symbol kinds, and empty for
/// <see cref="TokenKind.EndOfFile"/>.
/// </param>
/// <param name="Position">Where the token begins in the source.</param>
internal readonly record struct Token(TokenKind Kind, string Text, SourcePosition Position)
{
    /// <summary>Whether this token is an identifier whose text matches <paramref name="keyword"/>, case-insensitively.</summary>
    /// <param name="keyword">The keyword to test against.</param>
    public bool IsKeyword(string keyword) =>
        Kind == TokenKind.Identifier && string.Equals(Text, keyword, StringComparison.OrdinalIgnoreCase);
}
