using NSchema.Plan;
using NSchema.Plan.Model;
using NSchema.Policies;
using NSchema.Resolution;
using NSchema.Schema;
using NSchema.Schema.Model;
using NSchema.Scripts;
using NSchema.Scripts.Model;
using NSchema.State;

namespace NSchema.Operations.Services;

internal sealed class MigrationHelper(
    IMigrationPlanner planner,
    IEnumerable<IScriptProvider> scriptProviders,
    IKeyedResolver<IOperationReporter> reporters,
    ICurrentSchemaProvider currentProvider,
    IDesiredSchemaProvider desiredProvider,
    ISchemaStateStore? store = null
) : IMigrationHelper
{
    public bool HasStore => store is not null;

    public async Task<DatabaseSchema> Validate(string[]? schemas, CancellationToken cancellationToken = default)
    {
        reporters.Current.Info("Loading desired schema...");
        var desiredSchema = await desiredProvider.GetSchema(schemas, cancellationToken);

        reporters.Current.Info("Validating schema...");
        ReportOrThrow(planner.Validate(desiredSchema));

        return desiredSchema;
    }

    public async Task<MigrationPlan> Plan(SchemaSourceMode currentSource, bool required, string[]? schemas, CancellationToken cancellationToken = default)
    {
        reporters.Current.Info("Loading desired schema...");
        var desiredSchema = await desiredProvider.GetSchema(schemas, cancellationToken);
        var schemasInScope = schemas ?? desiredSchema.AllSchemaNames;

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
        return ReportOrThrow(planner.Plan(currentSchema, desiredSchema, scripts));
    }

    public async Task<MigrationPlan> PlanDestroy(CancellationToken cancellationToken = default)
    {
        // The managed schema is what we tear down: recorded state when we have it, otherwise the declared desired schema.
        // GetSchema applies the schema transformers, so a transform that adds an object is reflected here and therefore gets dropped.
        var managedSchema = store is not null
            ? await currentProvider.GetSchema(SchemaSourceMode.Offline, null, required: true, cancellationToken)
            : await desiredProvider.GetSchema(null, cancellationToken);

        reporters.Current.Info("Computing teardown plan...");
        return ReportOrThrow(planner.PlanTeardown(managedSchema));
    }

    /// <summary>
    /// Throws on schema-policy errors; otherwise reports any non-error diagnostics.
    /// </summary>
    private void ReportOrThrow(PolicyDiagnostics diagnostics)
    {
        if (diagnostics.HasErrors)
        {
            throw new PolicyViolationException(diagnostics.Errors.ToList());
        }

        if (diagnostics.Count > 0)
        {
            reporters.Current.ReportDiagnostics(diagnostics);
        }
    }

    /// <summary>
    /// Throws on planning errors; otherwise reports the diff, plan, and diagnostics, and returns the plan.
    /// </summary>
    private MigrationPlan ReportOrThrow(MigrationPlanResult result)
    {
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
