using NSchema.Plan;
using NSchema.Policies;
using NSchema.Resolution;
using NSchema.Schema;
using NSchema.Schema.Model;
using NSchema.Scripts;
using NSchema.Scripts.Model;
using NSchema.State;

namespace NSchema.Operations.Services;

internal sealed class MigrationWorkflow(
    IMigrationPlanner planner,
    IEnumerable<IScriptProvider> scriptProviders,
    IKeyedResolver<IOperationReporter> reporters,
    ICurrentSchemaProvider currentProvider,
    IDesiredSchemaProvider desiredProvider,
    ISchemaStateSerializer stateSerializer,
    ISchemaStateStore? store = null
) : IMigrationWorkflow
{
    public async Task<DatabaseSchema> Validate(string[]? schemas, CancellationToken cancellationToken = default)
    {
        reporters.Current.Info("Loading desired schema...");
        var desiredSchema = await desiredProvider.GetSchema(schemas, cancellationToken);

        reporters.Current.Info("Validating schema...");
        ReportOrThrow(planner.Validate(desiredSchema));

        return desiredSchema;
    }

    public async Task<PlannedMigration> Plan(SchemaSourceMode currentSource, bool required, string[]? schemas, CancellationToken cancellationToken = default)
    {
        reporters.Current.Info("Loading desired schema...");
        var desiredSchema = await desiredProvider.GetSchema(schemas, cancellationToken);
        var schemasInScope = schemas ?? desiredSchema.AllSchemaNames;

        reporters.Current.Info($"Migration will be scoped to the following schemas: {string.Join(", ", schemasInScope)}");

        reporters.Current.Info("Loading provider schema...");
        var currentSchema = await currentProvider.GetSchema(currentSource, schemasInScope, required, cancellationToken);

        reporters.Current.Info("Loading scripts...");
        List<Script> scripts = [];
        var scriptTasks = scriptProviders.Select(p => p.GetScripts(cancellationToken)).ToList();
        foreach (var task in scriptTasks)
        {
            scripts.AddRange(await task);
        }

        reporters.Current.Info("Computing migration plan...");
        return ReportOrThrow(planner.Plan(currentSchema, desiredSchema, scripts));
    }

    public async Task<PlannedMigration> PlanDestroy(CancellationToken cancellationToken = default)
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
    /// Throws on planning errors; otherwise reports the diff, plan, and diagnostics, and returns the planned migration.
    /// </summary>
    private PlannedMigration ReportOrThrow(MigrationPlanResult result)
    {
        // Show the diff first, even on error: a failing diff policy (e.g. the destructive-action
        // policy on a dropped table) is only actionable if the user can see the offending change.
        // A schema-policy failure has no diff (it's computed after that gate), so guard for null.
        if (result.Diff is not null)
        {
            reporters.Current.ReportDiff(result.Diff);
        }

        if (result.HasErrors)
        {
            throw new PolicyViolationException(result.Diagnostics.Errors.ToList());
        }

        reporters.Current.ReportPlan(result.Plan);
        reporters.Current.ReportDiagnostics(result.Diagnostics);

        return new PlannedMigration(result.Plan, result.Diff);
    }

    public async Task Refresh(RefreshMode mode, CancellationToken cancellationToken = default)
    {
        if (store is null)
        {
            if (mode == RefreshMode.Required)
            {
                throw new InvalidOperationException("Unable to refresh state without a configured state store.");
            }

            // Optional: nothing to capture.
            return;
        }

        reporters.Current.Info("Updating state store...");
        var schema = await currentProvider.GetSchema(SchemaSourceMode.Online, null, required: true, cancellationToken);
        var snapshot = stateSerializer.Serialize(schema);
        await store.Write(snapshot, cancellationToken);
        reporters.Current.Info("State store updated successfully.");
    }
}
