using NSchema.Diff.Model;
using NSchema.Plan.Model;

namespace NSchema.Operations.Services;

/// <summary>
/// The output of the workflow's planning step.
/// </summary>
/// <param name="Plan">The computed migration plan.</param>
/// <param name="Diff">The structured diff the plan was derived from.</param>
internal sealed record PlannedMigration(MigrationPlan Plan, DatabaseDiff Diff);
