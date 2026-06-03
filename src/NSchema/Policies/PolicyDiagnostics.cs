namespace NSchema.Policies;

/// <summary>
/// Represents a collection of diagnostics resulting from policy evaluations.
/// </summary>
public class PolicyDiagnostics : List<PolicyDiagnostic>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyDiagnostics"/> class with the specified diagnostics.
    /// </summary>
    public PolicyDiagnostics(IEnumerable<PolicyDiagnostic> diagnostics) : base(diagnostics) { }

    /// <summary>
    /// True when the diagnostics contain at least one error-severity finding.
    /// </summary>
    public bool HasErrors => Errors.Any();

    /// <summary>
    /// The subset of diagnostics with error severity.
    /// </summary>
    public IEnumerable<PolicyDiagnostic> Errors => this.Where(d => d.Severity == PolicyDiagnosticSeverity.Error);
}
