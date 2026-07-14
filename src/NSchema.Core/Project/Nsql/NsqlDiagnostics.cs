
namespace NSchema.Project.Nsql;

/// <summary>
/// The diagnostics minted when reading NSchema source.
/// </summary>
internal static class NsqlDiagnostics
{
    private const string Source = "syntax";

    /// <summary>
    /// A source document that could not be lexed or parsed.
    /// </summary>
    public static NsqlDiagnostic Syntax(NsqlSyntaxException exception) =>
        new(Source, exception.Message, DiagnosticSeverity.Error, exception.Position);

    /// <summary>
    /// A file that could not be read at all. A file-level finding has no position in the source; it points
    /// at the top of the file.
    /// </summary>
    public static NsqlDiagnostic UnreadableFile(string path, Exception exception) => new(
        Source,
        $"Could not read '{path}': {exception.Message}",
        DiagnosticSeverity.Error,
        new SourcePosition(0, 1, 1)
    )
    {
        File = path,
    };
}
