using NSchema.Project.Nsql.Syntax.Config;

namespace NSchema.Project.Nsql;

/// <summary>
/// A parsed lockfile (<c>nschema.lock</c>): the recorded plugin pins, in order.
/// </summary>
/// <param name="Statements">The lock statements in source order.</param>
public sealed record NsqlLockDocument(IReadOnlyList<ConfigStatement> Statements)
{
    /// <summary>
    /// The file the document was read from, or <see langword="null"/> when parsed from raw source.
    /// </summary>
    public string? FilePath { get; init; }
}
