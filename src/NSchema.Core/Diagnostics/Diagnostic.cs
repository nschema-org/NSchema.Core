namespace NSchema.Diagnostics;

/// <summary>
/// A single structured finding, <see cref="Result{T}"/>, and the policy diagnostics.
/// </summary>
/// <param name="Source">What produced this finding.</param>
/// <param name="Message">A descriptive message about the finding.</param>
/// <param name="Severity">The severity of the finding.</param>
public record Diagnostic(string Source, string Message, DiagnosticSeverity Severity)
{
    /// <summary>
    /// Creates an informational diagnostic.
    /// </summary>
    /// <param name="source">What produced this finding.</param>
    /// <param name="message">A descriptive message about the finding.</param>
    /// <returns>The created <see cref="Diagnostic"/>.</returns>
    public static Diagnostic Info(string source, string message) => new(source, message, DiagnosticSeverity.Info);

    /// <summary>
    /// Creates a warning diagnostic.
    /// </summary>
    /// <param name="source">What produced this finding.</param>
    /// <param name="message">A descriptive message about the finding.</param>
    /// <returns>The created <see cref="Diagnostic"/>.</returns>
    public static Diagnostic Warning(string source, string message) => new(source, message, DiagnosticSeverity.Warning);

    /// <summary>
    /// Creates an error diagnostic.
    /// </summary>
    /// <param name="source">What produced this finding.</param>
    /// <param name="message">A descriptive message about the finding.</param>
    /// <returns>The created <see cref="Diagnostic"/>.</returns>
    public static Diagnostic Error(string source, string message) => new(source, message, DiagnosticSeverity.Error);
}
