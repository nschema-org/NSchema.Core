using NSchema.Model;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax;

/// <summary>
/// <c>RENAME &lt;kind&gt; schema.name TO name;</c> for a schema-level object.
/// </summary>
/// <param name="Kind">The kind of object being renamed.</param>
/// <param name="From">The object's current address.</param>
/// <param name="To">The name the object is renamed to.</param>
public sealed record RenameObjectStatement(ObjectKind Kind, QualifiedName From, Identifier To) : NsqlStatement
{
    /// <summary>
    /// The <c>RENAME</c> keyword token, when parsed.
    /// </summary>
    public Token? RenameKeyword { get; init; }

    /// <summary>
    /// The keyword token(s) naming the kind (one, or <c>MATERIALIZED VIEW</c>), when parsed.
    /// </summary>
    public IReadOnlyList<Token> KindKeywords { get; init; } = [];

    /// <summary>
    /// The <c>TO</c> keyword token, when parsed.
    /// </summary>
    public Token? ToKeyword { get; init; }

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
            if (RenameKeyword is { } rename)
            {
                yield return rename;
            }
            foreach (var keyword in KindKeywords)
            {
                yield return keyword;
            }
            yield return From;
            if (ToKeyword is { } to)
            {
                yield return to;
            }
            yield return To;
            if (SemicolonToken is { } semicolon)
            {
                yield return semicolon;
            }
        }
    }
}
