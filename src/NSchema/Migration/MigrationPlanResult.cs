using System.Diagnostics.CodeAnalysis;
using NSchema.Diff.Model;
using NSchema.Plan.Model;
using NSchema.Policies;

namespace NSchema.Migration;

/// <summary>
/// The result of a planning pass: the computed plan, its structured diff, and all policy diagnostics.
/// </summary>
public sealed record MigrationPlanResult(MigrationPlan? Plan, MigrationDiff? Diff, IEnumerable<PolicyDiagnostic> diagnostics)
{
    /// <summary>
    /// The diagnostics emitted by policies during the planning pass, if any.
    /// </summary>
    public PolicyDiagnostics Diagnostics { get; } = new(diagnostics);

    /// <summary>
    /// True when <see cref="diagnostics"/> contains at least one error-severity finding.
    /// When false, both <see cref="Plan"/> and <see cref="Diff"/> are guaranteed non-null.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Plan))]
    [MemberNotNullWhen(false, nameof(Diff))]
    public bool HasErrors => Diagnostics.HasErrors;
}
