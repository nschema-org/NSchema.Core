using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Extensions;

/// <summary>
/// <c>CREATE EXTENSION name [VERSION 'version'];</c> — the name may be written bare or quoted
/// (<c>'uuid-ossp'</c>) since extension names commonly contain characters a bare identifier cannot.
/// </summary>
/// <param name="Name">The extension name.</param>
/// <param name="Version">The <c>VERSION</c> string, or <see langword="null"/>.</param>
public sealed record CreateExtensionStatement(Identifier Name, string? Version = null) : NsqlStatement
{
    /// <summary>
    /// The <c>CREATE</c> keyword token, when parsed.
    /// </summary>
    public Token? CreateKeyword { get; init; }

    /// <summary>
    /// The <c>EXTENSION</c> keyword token, when parsed.
    /// </summary>
    public Token? ExtensionKeyword { get; init; }

    /// <summary>
    /// The <c>VERSION</c> keyword token, when parsed with a version clause.
    /// </summary>
    public Token? VersionKeyword { get; init; }

    /// <summary>
    /// The version string-literal token, when parsed with a version clause.
    /// </summary>
    public Token? VersionToken { get; init; }

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
            if (ExtensionKeyword is { } extension)
            {
                yield return extension;
            }
            yield return Name;
            if (VersionKeyword is { } versionKeyword)
            {
                yield return versionKeyword;
            }
            if (VersionToken is { } versionToken)
            {
                yield return versionToken;
            }
            if (SemicolonToken is { } semicolon)
            {
                yield return semicolon;
            }
        }
    }
}
