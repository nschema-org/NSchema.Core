using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Sequences;

/// <summary>
/// The parenthesised options of a sequence declaration.
/// </summary>
/// <param name="As">The <c>AS</c> data type, or <see langword="null"/>.</param>
/// <param name="Start">The <c>START</c> value, or <see langword="null"/>.</param>
/// <param name="Increment">The <c>INCREMENT</c> value, or <see langword="null"/>.</param>
/// <param name="MinValue">The <c>MINVALUE</c> value, or <see langword="null"/>.</param>
/// <param name="MaxValue">The <c>MAXVALUE</c> value, or <see langword="null"/>.</param>
/// <param name="Cache">The <c>CACHE</c> value, or <see langword="null"/>.</param>
/// <param name="Cycle">Whether <c>CYCLE</c> is written.</param>
public sealed record SequenceOptionsClause(
    TypeName? As = null,
    long? Start = null,
    long? Increment = null,
    long? MinValue = null,
    long? MaxValue = null,
    long? Cache = null,
    bool Cycle = false
) : NsqlNode
{
    /// <summary>
    /// The <c>(</c> token, when parsed.
    /// </summary>
    public Token? OpenParenToken { get; init; }

    /// <summary>
    /// The verbatim options-interior span token, when parsed.
    /// </summary>
    public Token? InteriorToken { get; init; }

    /// <summary>
    /// The <c>)</c> token, when parsed.
    /// </summary>
    public Token? CloseParenToken { get; init; }

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            if (OpenParenToken is { } open)
            {
                yield return open;
            }
            if (InteriorToken is { } interior)
            {
                yield return interior;
            }
            if (CloseParenToken is { } close)
            {
                yield return close;
            }
        }
    }
}
