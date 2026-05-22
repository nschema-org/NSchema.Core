using NSchema.Migration.Actions;
using NSchema.Policies;

namespace NSchema.Migration;

public sealed class DefaultSchemaMigrator(
    ICurrentSchemaProvider currentProvider,
    IEnumerable<IDesiredSchemaProvider> desiredProviders,
    IEnumerable<IDeploymentScriptProvider> scriptProviders,
    ISchemaAggregator schemaAggregator,
    ISchemaComparer comparer,
    IEnumerable<ISchemaPolicy> schemaPolicies,
    IEnumerable<IMigrationPlanTransformer> planTransformers,
    IEnumerable<IMigrationPolicy> migrationPolicies
) : ISchemaMigrator
{
    public async Task<MigrationPlan> Plan(CancellationToken cancellationToken = default)
    {
        // Get desired schema state from all registered providers and merge.
        var schemas = await Task.WhenAll(desiredProviders.Select(p => p.GetSchema(cancellationToken)));
        var desiredSchema = schemaAggregator.Aggregate(schemas);

        // Run all registered schema validation policies.
        var schemaErrors = schemaPolicies.SelectMany(p => p.Validate(desiredSchema)).ToList();
        if (schemaErrors.Count > 0)
        {
            throw new PolicyViolationException(schemaErrors);
        }

        string[] schemasInScope = desiredSchema.Schemas.Select(s => s.Name)
            .Concat(desiredSchema.DroppedSchemas)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Get current schema state.
        var currentSchema = await currentProvider.GetSchema(schemasInScope, cancellationToken);

        // Diff the two schemas.
        var migrationPlan = comparer.Compare(currentSchema, desiredSchema);

        // Collect deployment scripts and inject into the plan.
        var providerList = scriptProviders.ToList();
        if (providerList.Count > 0)
        {
            var preLists = await Task.WhenAll(providerList.Select(p => p.GetPreDeploymentScripts(cancellationToken)));
            var postLists = await Task.WhenAll(providerList.Select(p => p.GetPostDeploymentScripts(cancellationToken)));
            var preActions = preLists.SelectMany(s => s).Select(MigrationAction (s) => new RunPreDeploymentScript(s));
            var postActions = postLists.SelectMany(s => s).Select(MigrationAction (s) => new RunPostDeploymentScript(s));
            migrationPlan = new MigrationPlan([.. preActions, .. migrationPlan.Actions, .. postActions]);
        }

        // Apply all registered plan transformers in order.
        migrationPlan = planTransformers.Aggregate(migrationPlan, (p, t) => t.Transform(p));

        // Run all registered action policies against the transformed plan.
        var migrationErrors = migrationPolicies.SelectMany(p => p.Validate(migrationPlan)).ToList();
        if (migrationErrors.Count > 0)
        {
            throw new PolicyViolationException(migrationErrors);
        }

        return migrationPlan;
    }
}
