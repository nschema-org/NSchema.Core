using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Templates;

/// <summary>
/// A <c>TEMPLATE name … BEGIN … END;</c> declaration.
/// </summary>
/// <param name="Name">The template name.</param>
public abstract record TemplateStatement(Identifier Name) : NsqlStatement
{
    /// <summary>
    /// The <c>TEMPLATE</c> keyword token, when parsed.
    /// </summary>
    public Token? TemplateKeyword { get; init; }

    /// <summary>
    /// The <c>FOR</c> keyword token, when parsed with an explicit kind.
    /// </summary>
    public Token? ForKeyword { get; init; }

    /// <summary>
    /// The <c>SCHEMA</c>/<c>TABLE</c> kind keyword token, when parsed with an explicit kind.
    /// </summary>
    public Token? KindKeyword { get; init; }

    /// <summary>
    /// The <c>BEGIN</c> keyword token, when parsed.
    /// </summary>
    public Token? BeginKeyword { get; init; }

    /// <summary>
    /// The <c>END</c> keyword token, when parsed.
    /// </summary>
    public Token? EndKeyword { get; init; }

    /// <summary>
    /// The terminating <c>;</c> token, when parsed.
    /// </summary>
    public Token? SemicolonToken { get; init; }
}
