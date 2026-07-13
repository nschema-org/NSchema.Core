namespace NSchema.Project.Ddl;

/// <summary>
/// The diagnostics minted when reading NSchema DDL.
/// </summary>
internal static class DdlDiagnostics
{
    private const string Source = "syntax";

    /// <summary>
    /// A source document that could not be lexed or parsed; the message carries the position.
    /// </summary>
    public static Diagnostic Syntax(DdlSyntaxException exception) => Diagnostic.Error(Source, exception.Message);
}
