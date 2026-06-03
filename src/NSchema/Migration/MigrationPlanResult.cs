using System.Diagnostics.CodeAnalysis;
using NSchema.Diff.Model;
using NSchema.Plan.Model;
using NSchema.Policies;

namespace NSchema.Migration;

/// <summary>
/// The result of a planning pass: the computed plan, its structured diff, and all policy diagnostics.
/// </summary>
public sealed record MigrationPlanResult(MigrationPlan? Plan, MigrationDiff? Diff, IReadOnlyList<PolicyError> Diagnostics)
{
    /// <summary>
    /// True when <see cref="Diagnostics"/> contains at least one error-severity finding.
    /// When false, both <see cref="Plan"/> and <see cref="Diff"/> are guaranteed non-null.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Plan))]
    [MemberNotNullWhen(false, nameof(Diff))]
    public bool HasErrors => Errors.Any();

    /// <summary>
    /// The subset of <see cref="Diagnostics"/> with error severity.
    /// </summary>
    public IEnumerable<PolicyError> Errors => Diagnostics.Where(d => d.Severity == PolicySeverity.Error);
}
