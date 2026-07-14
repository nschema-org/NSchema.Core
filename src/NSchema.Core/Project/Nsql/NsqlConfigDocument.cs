using NSchema.Project.Nsql.Syntax.Config;

namespace NSchema.Project.Nsql;

/// <summary>
/// A parsed configuration file: the config-grammar statements as written, in order.
/// </summary>
/// <param name="Statements">The configuration statements in source order.</param>
public sealed record NsqlConfigDocument(IReadOnlyList<ConfigStatement> Statements)
{
    /// <summary>
    /// The file the document was read from, or <see langword="null"/> when parsed from raw source.
    /// </summary>
    public string? FilePath { get; init; }
}
