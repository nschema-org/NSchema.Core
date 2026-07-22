using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Templates;

/// <summary>
/// <c>APPLY TEMPLATE name IN SCHEMA a[, b]…;</c>
/// </summary>
/// <param name="TemplateName">The applied template's name.</param>
/// <param name="Schemas">The target schemas.</param>
public sealed record ApplyTemplateStatement(Identifier TemplateName, SeparatedSyntaxList<Identifier> Schemas) : NsqlStatement
{
    /// <summary>
    /// The <c>APPLY</c> keyword token, when parsed.
    /// </summary>
    public Token? ApplyKeyword { get; init; }

    /// <summary>
    /// The <c>TEMPLATE</c> keyword token, when parsed.
    /// </summary>
    public Token? TemplateKeyword { get; init; }

    /// <summary>
    /// The <c>IN</c> keyword token, when parsed.
    /// </summary>
    public Token? InKeyword { get; init; }

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
            if (ApplyKeyword is { } apply)
            {
                yield return apply;
            }
            if (TemplateKeyword is { } template)
            {
                yield return template;
            }
            yield return TemplateName;
            if (InKeyword is { } inKeyword)
            {
                yield return inKeyword;
            }
            if (SchemaKeyword is { } schema)
            {
                yield return schema;
            }
            foreach (var child in Schemas.Children)
            {
                yield return child;
            }
            if (SemicolonToken is { } semicolon)
            {
                yield return semicolon;
            }
        }
    }
}
