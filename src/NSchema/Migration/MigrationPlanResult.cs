using NSchema.Migration.Plan;
using NSchema.Policies;

namespace NSchema.Migration;

/// <summary>
/// The result of a planning pass: the computed plan plus any non-fatal policy diagnostics
/// (warnings, info) that the pipeline should surface to the user without aborting.
/// </summary>
internal sealed record MigrationPlanResult(MigrationPlan? Plan, IReadOnlyList<PolicyError> Diagnostics);
