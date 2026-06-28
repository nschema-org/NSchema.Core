using NSchema.Diagnostics;

namespace NSchema.Operations.Validate;

/// <summary>
/// The result of a validation.
/// </summary>
/// <param name="Findings">Every diagnostic produced by validating the desired schema against the schema policies.</param>
public sealed record ValidateResult(IReadOnlyList<Diagnostic> Findings)
{
    /// <summary>
    /// The error-severity findings; the schema is invalid when any are present.
    /// </summary>
    public IEnumerable<Diagnostic> Errors => Findings.Where(d => d.Severity is DiagnosticSeverity.Error);

    /// <summary>
    /// Whether validation found any error-severity issue.
    /// </summary>
    public bool HasErrors => Errors.Any();
}
