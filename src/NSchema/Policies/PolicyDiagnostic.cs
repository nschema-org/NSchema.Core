namespace NSchema.Policies;

/// <summary>
/// Represents a finding from a policy check against a schema or migration plan.
/// </summary>
/// <param name="PolicyName">The name of the policy that produced this finding.</param>
/// <param name="Message">A descriptive message about the finding.</param>
/// <param name="Severity">The severity of the finding.</param>
public record PolicyDiagnostic(string PolicyName, string Message, PolicyDiagnosticSeverity Severity)
{
    /// <summary>
    /// Creates an informational diagnostic.
    /// </summary>
    /// <param name="policyName">The name of the policy that produced this finding.</param>
    /// <param name="message">A descriptive message about the finding.</param>
    /// <returns>The created <see cref="PolicyDiagnostic"/>.</returns>
    public static PolicyDiagnostic Info(string policyName, string message) => new(policyName, message, PolicyDiagnosticSeverity.Info);

    /// <summary>
    /// Creates a warning diagnostic.
    /// </summary>
    /// <param name="policyName">The name of the policy that produced this finding.</param>
    /// <param name="message">A descriptive message about the finding.</param>
    /// <returns>The created <see cref="PolicyDiagnostic"/>.</returns>
    public static PolicyDiagnostic Warning(string policyName, string message) => new(policyName, message, PolicyDiagnosticSeverity.Warning);

    /// <summary>
    /// Creates an error diagnostic.
    /// </summary>
    /// <param name="policyName">The name of the policy that produced this finding.</param>
    /// <param name="message">A descriptive message about the finding.</param>
    /// <returns>The created <see cref="PolicyDiagnostic"/>.</returns>
    public static PolicyDiagnostic Error(string policyName, string message) => new(policyName, message, PolicyDiagnosticSeverity.Error);
}
