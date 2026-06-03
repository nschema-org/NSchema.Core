using NSchema.Migration.Diff;
using NSchema.Migration.Plan;
using NSchema.Policies;
using NSchema.Schema;

namespace NSchema.Migration;

/// <summary>
/// Default <see cref="IMigrationPlanner"/>. Runs the planning pipeline as a sequence of stages, each of which
/// transforms its representation and then validates it: the desired schema, the structured diff, and finally the
/// executable plan. The comparer produces the diff directly; the linearizer derives the ordered plan from it.
/// </summary>
/// <param name="scriptProviders">Pre- and post-deployment scripts to inject around the plan actions.</param>
/// <param name="comparer">Produces the structured diff by comparing the two schemas.</param>
/// <param name="linearizer">Derives the ordered executable plan from the diff.</param>
/// <param name="schemaTransformers">Transformers applied to the desired schema before diffing, in registration order.</param>
/// <param name="schemaPolicies">Policies that validate the desired schema before diffing.</param>
/// <param name="diffTransformers">Transformers applied to the diff before linearization, in registration order.</param>
/// <param name="diffPolicies">Policies that validate the structured diff (e.g. destructive-change checks).</param>
/// <param name="planTransformers">Transformers applied to the linearized plan in registration order.</param>
/// <param name="migrationPolicies">Policies that validate the final transformed plan.</param>
internal sealed class DefaultMigrationPlanner(
    IEnumerable<IScriptProvider> scriptProviders,
    ISchemaComparer comparer,
    IMigrationLinearizer linearizer,
    IEnumerable<ISchemaTransformer> schemaTransformers,
    IEnumerable<ISchemaPolicy> schemaPolicies,
    IEnumerable<IDiffTransformer> diffTransformers,
    IEnumerable<IDiffPolicy> diffPolicies,
    IEnumerable<IMigrationPlanTransformer> planTransformers,
    IEnumerable<IMigrationPolicy> migrationPolicies
) : IMigrationPlanner
{
    public async Task<MigrationPlanResult> Plan(DatabaseSchema currentSchema, DatabaseSchema desiredSchema, CancellationToken cancellationToken = default)
    {
        // Stage 1 — desired schema: transform, then validate. A schema-policy failure is fatal and skips the rest.
        var desired = schemaTransformers.Aggregate(desiredSchema, (schema, transformer) => transformer.Transform(schema));
        var schemaErrors = schemaPolicies.SelectMany(p => p.Validate(desired)).ToList();
        if (schemaErrors.Count > 0)
        {
            return new MigrationPlanResult(null, null, schemaErrors);
        }

        // Stage 2 — diff: the comparer produces the structured diff directly.
        var diff = comparer.Compare(currentSchema, desired);

        // Collect deployment scripts. Their names ride on the diff for rendering; they are spliced into the plan as
        // RunScript actions after linearization.
        var scripts = await CollectScripts(cancellationToken);
        if (scripts.Count > 0)
        {
            diff = diff with
            {
                PreDeploymentScripts = [.. scripts.Where(s => s.Type == ScriptType.PreDeployment).Select(s => s.Name)],
                PostDeploymentScripts = [.. scripts.Where(s => s.Type == ScriptType.PostDeployment).Select(s => s.Name)],
            };
        }

        diff = diffTransformers.Aggregate(diff, (d, t) => t.Transform(d));
        var diagnostics = diffPolicies.SelectMany(p => p.Validate(diff)).ToList();

        // Stage 3 — executable plan: linearize, splice scripts (pre first, post last), transform, validate.
        var plan = linearizer.Linearize(diff, desired);
        if (scripts.Count > 0)
        {
            var preActions = scripts.Where(s => s.Type == ScriptType.PreDeployment).Select(MigrationAction (s) => new RunScript(s));
            var postActions = scripts.Where(s => s.Type == ScriptType.PostDeployment).Select(MigrationAction (s) => new RunScript(s));
            plan = plan with { Actions = [.. preActions, .. plan.Actions, .. postActions] };
        }

        plan = planTransformers.Aggregate(plan, (p, t) => t.Transform(p));
        diagnostics.AddRange(migrationPolicies.SelectMany(p => p.Validate(plan)));

        return new MigrationPlanResult(plan, diff, diagnostics);
    }

    private async Task<List<Script>> CollectScripts(CancellationToken cancellationToken)
    {
        var providerList = scriptProviders.ToList();
        if (providerList.Count == 0)
        {
            return [];
        }

        var scriptLists = await Task.WhenAll(providerList.Select(p => p.GetScripts(cancellationToken)));
        return scriptLists.SelectMany(s => s).ToList();
    }
}
