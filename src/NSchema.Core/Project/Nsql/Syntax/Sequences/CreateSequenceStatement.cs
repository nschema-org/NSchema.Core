using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Sequences;

/// <summary>
/// <c>CREATE SEQUENCE schema.name [RENAMED FROM old] [(options)];</c>
/// </summary>
/// <param name="Name">The sequence name as written.</param>
/// <param name="Options">The options clause, or <see langword="null"/> when absent.</param>
public sealed record CreateSequenceStatement(
    QualifiedName Name,
    SequenceOptionsClause? Options = null
) : NsqlStatement
{
    /// <summary>
    /// The <c>CREATE</c> keyword token, when parsed.
    /// </summary>
    public Token? CreateKeyword { get; init; }

    /// <summary>
    /// The <c>SEQUENCE</c> keyword token, when parsed.
    /// </summary>
    public Token? SequenceKeyword { get; init; }

    /// <summary>
    /// The terminating <c>;</c> token, when parsed.
    /// </summary>
    public Token? SemicolonToken { get; init; }

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            if (DocComment is { } doc)
            {
                yield return doc;
            }
            if (CreateKeyword is { } create)
            {
                yield return create;
            }
            if (SequenceKeyword is { } sequence)
            {
                yield return sequence;
            }
            yield return Name;
            if (Options is { } options)
            {
                yield return options;
            }
            if (SemicolonToken is { } semicolon)
            {
                yield return semicolon;
            }
        }
    }
}
