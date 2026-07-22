using NSchema.Model;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Views;

/// <summary>
/// <c>CREATE [MATERIALIZED] VIEW schema.name [RENAMED FROM old] AS body;</c>
/// </summary>
/// <param name="Name">The view name as written.</param>
/// <param name="Body">The defining query, verbatim (the text after <c>AS</c>).</param>
/// <param name="IsMaterialized">Whether the view is materialized.</param>
public sealed record CreateViewStatement(
    QualifiedName Name,
    SqlText Body,
    bool IsMaterialized = false
) : NsqlStatement
{
    /// <summary>
    /// The <c>CREATE</c> keyword token, when parsed.
    /// </summary>
    public Token? CreateKeyword { get; init; }

    /// <summary>
    /// The <c>MATERIALIZED</c> keyword token, when parsed materialized.
    /// </summary>
    public Token? MaterializedKeyword { get; init; }

    /// <summary>
    /// The <c>VIEW</c> keyword token, when parsed.
    /// </summary>
    public Token? ViewKeyword { get; init; }

    /// <summary>
    /// The <c>AS</c> keyword token, when parsed.
    /// </summary>
    public Token? AsKeyword { get; init; }

    /// <summary>
    /// The verbatim view-body span token, when parsed.
    /// </summary>
    public Token? BodyToken { get; init; }

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
            if (MaterializedKeyword is { } materialized)
            {
                yield return materialized;
            }
            if (ViewKeyword is { } view)
            {
                yield return view;
            }
            yield return Name;
            if (AsKeyword is { } asKeyword)
            {
                yield return asKeyword;
            }
            if (BodyToken is { } body)
            {
                yield return body;
            }
            if (SemicolonToken is { } semicolon)
            {
                yield return semicolon;
            }
        }
    }
}
