using NSchema.Migration.Plan;
using NSchema.Policies;
using NSchema.Schema;

namespace NSchema.Migration;

/// <summary>
/// Default <see cref="IMigrationPlanner"/>.
/// </summary>
/// <param name="scriptProviders">Pre- and post-deployment scripts to inject around the plan actions.</param>
/// <param name="comparer">Produces the initial set of migration actions by diffing the two schemas.</param>
/// <param name="schemaPolicies">Policies that validate the desired schema before diffing.</param>
/// <param name="planTransformers">Transformers applied to the plan in registration order.</param>
/// <param name="migrationPolicies">Policies that validate the final transformed plan.</param>
internal sealed class DefaultMigrationPlanner(
    IEnumerable<IScriptProvider> scriptProviders,
    ISchemaComparer comparer,
    IEnumerable<ISchemaPolicy> schemaPolicies,
    IEnumerable<IMigrationPlanTransformer> planTransformers,
    IEnumerable<IMigrationPolicy> migrationPolicies
) : IMigrationPlanner
{
    public async Task<MigrationPlanResult> Plan(DatabaseSchema currentSchema, DatabaseSchema desiredSchema, CancellationToken cancellationToken = default)
    {
        // Validate the desired schema before diffing.
        var schemaErrors = schemaPolicies.SelectMany(p => p.Validate(desiredSchema)).ToList();
        if (schemaErrors.Count > 0)
        {
            return new MigrationPlanResult(null, schemaErrors);
        }

        // Diff the two schemas.
        var migrationPlan = comparer.Compare(currentSchema, desiredSchema);

        // Collect deployment scripts and inject into the plan.
        var providerList = scriptProviders.ToList();
        if (providerList.Count > 0)
        {
            var scriptLists = await Task.WhenAll(providerList.Select(p => p.GetScripts(cancellationToken)));
            var scripts = scriptLists.SelectMany(s => s).ToList();
            var preActions = scripts.Where(s => s.Type == ScriptType.PreDeployment).Select(MigrationAction (s) => new RunScript(s));
            var postActions = scripts.Where(s => s.Type == ScriptType.PostDeployment).Select(MigrationAction (s) => new RunScript(s));
            migrationPlan = migrationPlan with { Actions = [.. preActions, .. migrationPlan.Actions, .. postActions] };
        }

        // Apply all registered plan transformers in order.
        migrationPlan = planTransformers.Aggregate(migrationPlan, (p, t) => t.Transform(p));

        // Validate the transformed plan.
        var diagnostics = migrationPolicies.SelectMany(p => p.Validate(migrationPlan)).ToList();
        return new MigrationPlanResult(migrationPlan, diagnostics);
    }
}
