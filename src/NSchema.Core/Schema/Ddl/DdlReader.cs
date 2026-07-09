using NSchema.Schema.Ddl.Model;

namespace NSchema.Schema.Ddl;

/// <summary>
/// Reads NSchema DDL source text into the domain model. Stateless and thread-safe; use <see cref="Instance"/>.
/// </summary>
public sealed class DdlReader
{
    /// <summary>
    /// The singleton instance of <see cref="DdlReader"/> for convenience.
    /// </summary>
    public static readonly DdlReader Instance = new();

    /// <summary>
    /// Reads <paramref name="source"/> into a <see cref="DdlDocument"/>.
    /// </summary>
    /// <param name="source">The NSchema DDL document to read.</param>
    public DdlDocument Read(string source) => new DdlParser(source).Parse().Document;
}
