using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Schemas;

/// <summary>
/// <c>RENAME SCHEMA name TO name;</c>
/// </summary>
/// <param name="From">The schema's current name.</param>
/// <param name="To">The name the schema is renamed to.</param>
public sealed record RenameSchemaStatement(Identifier From, Identifier To) : NsqlStatement
{
    /// <summary>
    /// The <c>RENAME</c> keyword token, when parsed.
    /// </summary>
    public Token? RenameKeyword { get; init; }

    /// <summary>
    /// The <c>SCHEMA</c> keyword token, when parsed.
    /// </summary>
    public Token? SchemaKeyword { get; init; }

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
            if (SchemaKeyword is { } schema)
            {
                yield return schema;
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
