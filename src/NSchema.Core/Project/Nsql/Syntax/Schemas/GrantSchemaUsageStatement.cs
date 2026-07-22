using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Schemas;

/// <summary>
/// <c>GRANT USAGE ON SCHEMA name TO role;</c>
/// </summary>
/// <param name="Schema">The schema granted on.</param>
/// <param name="Role">The role granted to.</param>
public sealed record GrantSchemaUsageStatement(Identifier Schema, Identifier Role) : NsqlStatement
{
    /// <summary>
    /// The <c>GRANT</c> keyword token, when parsed.
    /// </summary>
    public Token? GrantKeyword { get; init; }

    /// <summary>
    /// The <c>USAGE</c> keyword token, when parsed.
    /// </summary>
    public Token? UsageKeyword { get; init; }

    /// <summary>
    /// The <c>ON</c> keyword token, when parsed.
    /// </summary>
    public Token? OnKeyword { get; init; }

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
            if (GrantKeyword is { } grant)
            {
                yield return grant;
            }
            if (UsageKeyword is { } usage)
            {
                yield return usage;
            }
            if (OnKeyword is { } on)
            {
                yield return on;
            }
            if (SchemaKeyword is { } schemaKeyword)
            {
                yield return schemaKeyword;
            }
            yield return Schema;
            if (ToKeyword is { } to)
            {
                yield return to;
            }
            yield return Role;
            if (SemicolonToken is { } semicolon)
            {
                yield return semicolon;
            }
        }
    }
}
