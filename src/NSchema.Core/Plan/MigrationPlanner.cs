using NSchema.Diff;
using NSchema.Plan.Model;
using NSchema.Policies;
using NSchema.Schema;
using NSchema.Schema.Model;
using NSchema.Scripts.Model;

namespace NSchema.Plan;

/// <summary>
/// Default <see cref="IMigrationPlanner"/>.
/// </summary>
/// <param name="comparer">Produces the structured diff by comparing the two schemas.</param>
/// <param name="linearizer">Derives the ordered executable plan from the diff.</param>
/// <param name="schemaPolicies">Policies that validate the desired schema.</param>
/// <param name="diffTransformers">Transformers applied to the diff before linearization, in registration order.</param>
/// <param name="diffPolicies">Policies that validate the structured diff (e.g. destructive-change checks).</param>
/// <param name="planTransformers">Transformers applied to the linearized plan in registration order.</param>
/// <param name="planPolicies">Policies that validate the final transformed plan.</param>
internal sealed class MigrationPlanner(
    ISchemaComparer comparer,
    IPlanLinearizer linearizer,
    IEnumerable<ISchemaPolicy> schemaPolicies,
    IEnumerable<IDiffTransformer> diffTransformers,
    IEnumerable<IDiffPolicy> diffPolicies,
    IEnumerable<IPlanTransformer> planTransformers,
    IEnumerable<IPlanPolicy> planPolicies
) : IMigrationPlanner
{
    public PolicyDiagnostics Validate(DatabaseSchema desiredSchema) =>
        new(schemaPolicies.SelectMany(p => p.Validate(desiredSchema)));

    public MigrationPlanResult Plan(DatabaseSchema currentSchema, DatabaseSchema desiredSchema, IReadOnlyList<Script> scripts)
    {
        // Validate the desired schema. A schema-policy error is fatal and skips the rest.
        var schemaDiagnostics = Validate(desiredSchema);
        if (schemaDiagnostics.HasErrors)
        {
            return new MigrationPlanResult(null, null, schemaDiagnostics);
        }

        // Diff the schemas.
        var diff = comparer.Compare(currentSchema, desiredSchema);

        // Transform and validate the diff.
        diff = diffTransformers.Aggregate(diff, (d, t) => t.Transform(d));
        var diagnostics = new List<PolicyDiagnostic>(schemaDiagnostics);
        diagnostics.AddRange(diffPolicies.SelectMany(p => p.Validate(diff)));

        // Convert the diff into a migration plan.
        var actions = linearizer.Linearize(diff);
        var preDeploymentScripts = scripts.Where(s => s.Type == ScriptType.PreDeployment).ToList();
        var postDeploymentScripts = scripts.Where(s => s.Type == ScriptType.PostDeployment).ToList();
        var plan = new MigrationPlan(actions, preDeploymentScripts, postDeploymentScripts);

        // Transform and validate the plan.
        plan = planTransformers.Aggregate(plan, (p, t) => t.Transform(p));
        diagnostics.AddRange(planPolicies.SelectMany(p => p.Validate(plan)));

        return new MigrationPlanResult(plan, diff, diagnostics);
    }

    public MigrationPlanResult PlanTeardown(DatabaseSchema currentSchema)
    {
        // Don't run transformers/policies for teardown.
        // This is a purely destructive action, and needs to be available as an escape.
        var diff = comparer.Compare(currentSchema, new DatabaseSchema());
        var actions = linearizer.Linearize(diff);
        var plan = new MigrationPlan(actions, [], []);
        return new MigrationPlanResult(plan, diff, []);
    }
}
