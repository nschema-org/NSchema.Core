using NSchema.Project.Ddl.Models;
using NSchema.Project.Nsql;

namespace NSchema.Project.Ddl;

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
    public Result<DdlDocument> Read(string source)
    {
        var parser = new NsqlParser(source);
        var document = parser.Parse();
        if (parser.Errors.Count > 0)
        {
            // The parser recovers at statement boundaries, so every syntax error in the document is
            // reported at once.
            return Result.Failure<DdlDocument>(parser.Errors.Select(DdlDiagnostics.Syntax));
        }

        try
        {
            return DocumentProjector.Project(document);
        }
        catch (DdlSyntaxException ex)
        {
            return Result.Failure<DdlDocument>(DdlDiagnostics.Syntax(ex));
        }
    }
}
