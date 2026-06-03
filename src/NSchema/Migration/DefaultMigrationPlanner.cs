using NSchema.Diff;
using NSchema.Plan;
using NSchema.Plan.Model;
using NSchema.Schema.Model;
using NSchema.Scripts.Model;

namespace NSchema.Migration;

/// <summary>
/// Default <see cref="IMigrationPlanner"/>.
/// </summary>
/// <param name="comparer">Produces the structured diff by comparing the two schemas.</param>
/// <param name="linearizer">Derives the ordered executable plan from the diff.</param>
/// <param name="diffTransformers">Transformers applied to the diff before linearization, in registration order.</param>
/// <param name="diffPolicies">Policies that validate the structured diff (e.g. destructive-change checks).</param>
/// <param name="planTransformers">Transformers applied to the linearized plan in registration order.</param>
/// <param name="migrationPolicies">Policies that validate the final transformed plan.</param>
internal sealed class DefaultMigrationPlanner(
    ISchemaComparer comparer,
    IMigrationLinearizer linearizer,
    IEnumerable<IDiffTransformer> diffTransformers,
    IEnumerable<IDiffPolicy> diffPolicies,
    IEnumerable<IMigrationPlanTransformer> planTransformers,
    IEnumerable<IMigrationPolicy> migrationPolicies
) : IMigrationPlanner
{
    public MigrationPlanResult Plan(DatabaseSchema currentSchema, DatabaseSchema desiredSchema, IReadOnlyList<Script> scripts)
    {
        // Diff the schemas.
        var diff = comparer.Compare(currentSchema, desiredSchema);
        if (scripts.Count > 0)
        {
            diff = diff with
            {
                PreDeploymentScripts = [.. scripts.Where(s => s.Type == ScriptType.PreDeployment)],
                PostDeploymentScripts = [.. scripts.Where(s => s.Type == ScriptType.PostDeployment)],
            };
        }

        // Transform and validate the diff.
        diff = diffTransformers.Aggregate(diff, (d, t) => t.Transform(d));
        var diagnostics = diffPolicies.SelectMany(p => p.Validate(diff)).ToList();

        // Convert the diff into a migration plan.
        var plan = linearizer.Linearize(diff);
        var preActions = diff.PreDeploymentScripts.Select(MigrationAction (s) => new RunScript(s));
        var postActions = diff.PostDeploymentScripts.Select(MigrationAction (s) => new RunScript(s));
        plan = plan with { Actions = [.. preActions, .. plan.Actions, .. postActions] };

        // Transform and validate the plan.
        plan = planTransformers.Aggregate(plan, (p, t) => t.Transform(p));
        diagnostics.AddRange(migrationPolicies.SelectMany(p => p.Validate(plan)));

        return new MigrationPlanResult(plan, diff, diagnostics);
    }
}
