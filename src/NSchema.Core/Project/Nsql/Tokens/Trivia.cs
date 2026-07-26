namespace NSchema.Project.Nsql.Tokens;

/// <summary>
/// A span of insignificant source text (whitespace or a comment) attached to a token,
/// so the tree prints back byte-for-byte. <see cref="Text"/> is verbatim.
/// </summary>
/// <param name="Kind">The kind of trivia.</param>
/// <param name="Text">The verbatim source text.</param>
/// <param name="Position">Where the trivia begins in the source.</param>
public readonly record struct Trivia(TriviaKind Kind, string Text, SourcePosition Position)
{
    /// <summary>
    /// A synthetic line break.
    /// </summary>
    public static Trivia LineBreak => new(TriviaKind.EndOfLine, "\n", SourcePosition.None);

    /// <summary>
    /// A synthetic line comment.
    /// </summary>
    /// <param name="text">The comment text, <c>--</c> markers included.</param>
    public static Trivia Comment(string text) => new(TriviaKind.LineComment, text.TrimEnd(), SourcePosition.None);

    /// <summary>
    /// Whether this trivia is a comment (as opposed to whitespace or a line break).
    /// </summary>
    public bool IsComment => Kind is TriviaKind.LineComment or TriviaKind.BlockComment;
}
