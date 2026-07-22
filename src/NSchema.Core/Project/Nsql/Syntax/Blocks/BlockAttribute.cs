using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Blocks;

/// <summary>
/// A single <c>key = value</c> attribute of a block; the key may be dotted (<c>pool.max</c>).
/// </summary>
/// <param name="Key">The attribute key as written.</param>
/// <param name="Value">The attribute value as written; the binder converts it to the target type.</param>
public sealed record BlockAttribute(string Key, string Value) : NsqlNode
{
    /// <summary>
    /// The verbatim (possibly dotted) key span token, when parsed.
    /// </summary>
    public Token? KeyToken { get; init; }

    /// <summary>
    /// The <c>=</c> token, when parsed.
    /// </summary>
    public Token? EqualsToken { get; init; }

    /// <summary>
    /// The verbatim value span token, when parsed.
    /// </summary>
    public Token? ValueToken { get; init; }

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            if (KeyToken is { } key)
            {
                yield return key;
            }
            if (EqualsToken is { } equals)
            {
                yield return equals;
            }
            if (ValueToken is { } value)
            {
                yield return value;
            }
        }
    }
}
