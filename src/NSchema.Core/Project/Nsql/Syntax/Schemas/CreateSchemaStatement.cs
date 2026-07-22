using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Schemas;

/// <summary>
/// <c>CREATE [PARTIAL] SCHEMA name [RENAMED FROM old];</c>
/// </summary>
/// <param name="Name">The schema name.</param>
public sealed record CreateSchemaStatement(Identifier Name) : NsqlStatement
{
    /// <summary>
    /// The <c>CREATE</c> keyword token, when parsed.
    /// </summary>
    public Token? CreateKeyword { get; init; }

    /// <summary>
    /// The <c>SCHEMA</c> keyword token, when parsed.
    /// </summary>
    public Token? SchemaKeyword { get; init; }

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
            if (SchemaKeyword is { } schema)
            {
                yield return schema;
            }
            yield return Name;
            if (SemicolonToken is { } semicolon)
            {
                yield return semicolon;
            }
        }
    }
}
