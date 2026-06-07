using System.Diagnostics.CodeAnalysis;
using NSchema.Diff.Model;
using NSchema.Plan.Model;
using NSchema.Policies;

namespace NSchema.Migration;

/// <summary>
/// The result of a planning pass: the computed plan, its structured diff, and all policy diagnostics.
/// </summary>
public sealed record MigrationPlanResult
{
    /// <param name="plan">The computed migration plan, or <c>null</c> when planning produced errors.</param>
    /// <param name="diff">The structured diff, or <c>null</c> when planning produced errors.</param>
    /// <param name="diagnostics">The diagnostics emitted by policies during the planning pass.</param>
    public MigrationPlanResult(MigrationPlan? plan, DatabaseDiff? diff, IEnumerable<PolicyDiagnostic> diagnostics)
    {
        Plan = plan;
        Diff = diff;
        Diagnostics = new PolicyDiagnostics(diagnostics);
    }

    /// <summary>The computed migration plan; non-null when <see cref="HasErrors"/> is <c>false</c>.</summary>
    public MigrationPlan? Plan { get; }

    /// <summary>The structured diff; non-null when <see cref="HasErrors"/> is <c>false</c>.</summary>
    public DatabaseDiff? Diff { get; }

    /// <summary>
    /// The diagnostics emitted by policies during the planning pass, if any.
    /// </summary>
    public PolicyDiagnostics Diagnostics { get; }

    /// <summary>
    /// True when <see cref="Diagnostics"/> contains at least one error-severity finding.
    /// When false, both <see cref="Plan"/> and <see cref="Diff"/> are guaranteed non-null.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Plan))]
    [MemberNotNullWhen(false, nameof(Diff))]
    public bool HasErrors => Diagnostics.HasErrors;
}
