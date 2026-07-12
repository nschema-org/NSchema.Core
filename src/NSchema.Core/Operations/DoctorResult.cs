
namespace NSchema.Operations;

/// <summary>
/// The result of running diagnostics.
/// </summary>
/// <param name="Checks">Every diagnostic produced by the infrastructure probes, one per check.</param>
public sealed record DoctorResult(IReadOnlyList<Diagnostic> Checks)
{
    /// <summary>
    /// The error-severity checks; the configured infrastructure is unhealthy when any are present.
    /// </summary>
    public IEnumerable<Diagnostic> Errors => Checks.Where(d => d.Severity is DiagnosticSeverity.Error);

    /// <summary>
    /// Whether any check failed (an error-severity probe result).
    /// </summary>
    public bool HasErrors => Errors.Any();
}
