using Microsoft.Extensions.Options;
using NSchema.Migration;
using NSchema.Plan.Model;
using NSchema.Policies;
using NSchema.Resolution;
using NSchema.Schema;
using NSchema.Schema.Model;
using NSchema.Scripts;
using NSchema.Scripts.Model;
using NSchema.State;

namespace NSchema.Hosting.Services;

internal sealed class MigrationHelper(
    IOptions<MigrationOptions> options,
    IMigrationPlanner planner,
    IEnumerable<IScriptProvider> scriptProviders,
    IKeyedResolver<IMigrationReporter> reporters,
    ICurrentSchemaProvider currentProvider,
    IDesiredSchemaProvider desiredProvider,
    IEnumerable<ISchemaPolicy> schemaPolicies,
    ISchemaStateStore? store = null
) : IMigrationHelper
{
    public bool HasStore => store is not null;

    public async Task<DatabaseSchema> Validate(CancellationToken cancellationToken = default)
    {
        reporters.Current.Info("Loading desired schema...");
        var desiredSchema = await desiredProvider.GetSchema(options.Value.SchemaNames, cancellationToken);

        reporters.Current.Info("Validating schema...");
        var schemaDiagnostics = new PolicyDiagnostics(schemaPolicies.SelectMany(p => p.Validate(desiredSchema)));
        if (schemaDiagnostics.HasErrors)
        {
            throw new PolicyViolationException(schemaDiagnostics.Errors.ToList());
        }

        if (schemaDiagnostics.Count > 0)
        {
            reporters.Current.ReportDiagnostics(schemaDiagnostics);
        }

        return desiredSchema;
    }

    public async Task<MigrationPlan> Plan(SchemaSourceMode currentSource, bool required, CancellationToken cancellationToken = default)
    {
        var desiredSchema = await Validate(cancellationToken);
        var schemasInScope = options.Value.SchemaNames ?? desiredSchema.AllSchemaNames;

        reporters.Current.Info($"Migration will be scoped to the following schemas: {string.Join(", ", schemasInScope)}");

        reporters.Current.Info("Loading provider schema...");
        var currentSchema = await currentProvider.GetSchema(currentSource, schemasInScope, required, cancellationToken);

        reporters.Current.Info("Loading scripts...");
        List<Script> scripts = [];
        var scriptTasks = scriptProviders.Select(p => p.GetScripts(cancellationToken));
        foreach (var task in scriptTasks)
        {
            scripts.AddRange(await task);
        }

        reporters.Current.Info("Computing migration plan...");
        var result = planner.Plan(currentSchema, desiredSchema, scripts);
        if (result.HasErrors)
        {
            throw new PolicyViolationException(result.Diagnostics.Errors.ToList());
        }

        reporters.Current.ReportDiff(result.Diff);
        reporters.Current.ReportPlan(result.Plan);
        reporters.Current.ReportDiagnostics(result.Diagnostics);

        return result.Plan;
    }

    public async Task<MigrationPlan> PlanDestroy(CancellationToken cancellationToken = default)
    {
        // The managed schema is what we tear down: recorded state when we have it, otherwise the declared desired schema.
        // GetSchema applies the schema transformers, so a transform that adds an object is reflected here and therefore gets dropped.
        var managedSchema = store is not null
            ? await currentProvider.GetSchema(SchemaSourceMode.Offline, options.Value.SchemaNames, required: true, cancellationToken)
            : await desiredProvider.GetSchema(options.Value.SchemaNames, cancellationToken);

        reporters.Current.Info("Computing teardown plan...");
        var result = planner.PlanTeardown(managedSchema);
        if (result.HasErrors)
        {
            throw new PolicyViolationException(result.Diagnostics.Errors.ToList());
        }

        reporters.Current.ReportDiff(result.Diff);
        reporters.Current.ReportPlan(result.Plan);
        reporters.Current.ReportDiagnostics(result.Diagnostics);

        return result.Plan;
    }

    public async Task Refresh(CancellationToken cancellationToken = default)
    {
        if (store == null)
        {
            throw new InvalidOperationException("Unable to refresh state without backend store.");
        }

        var schema = await currentProvider.GetSchema(SchemaSourceMode.Online, null, required: true, cancellationToken);
        await store.Write(schema, cancellationToken);
    }
}
