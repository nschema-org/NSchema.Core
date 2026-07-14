namespace NSchema.Project.Nsql;

/// <summary>
/// A finding from reading NSchema source.
/// </summary>
/// <param name="Source">What produced this finding.</param>
/// <param name="Message">A descriptive message about the finding.</param>
/// <param name="Severity">The severity of the finding.</param>
/// <param name="Position">The position in the source where the finding was detected.</param>
public sealed record NsqlDiagnostic(string Source, string Message, DiagnosticSeverity Severity, SourcePosition Position)
    : Diagnostic(Source, Message, Severity)
{
    /// <summary>
    /// The file the source was read from, or <see langword="null"/> when parsed from raw source.
    /// </summary>
    public string? File { get; init; }
}
