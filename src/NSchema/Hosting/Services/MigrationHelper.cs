using Microsoft.Extensions.Options;
using NSchema.Migration;
using NSchema.Plan.Model;
using NSchema.Policies;
using NSchema.Schema;
using NSchema.Scripts;
using NSchema.State;

namespace NSchema.Hosting.Services;

internal sealed class MigrationHelper(
    IOptions<MigrationOptions> options,
    IMigrationPlanner planner,
    IEnumerable<IScriptProvider> scriptProviders,
    IMigrationReporter reporter,
    ICurrentSchemaProvider currentProvider,
    IDesiredSchemaProvider desiredProvider,
    IEnumerable<ISchemaPolicy> schemaPolicies,
    ISchemaStateStore? store = null
) : IMigrationHelper
{
    public bool HasStore => store is not null;

    public async Task<MigrationPlan> Plan(SchemaSourceMode currentSource, bool required, CancellationToken cancellationToken = default)
    {
        reporter.Info("Loading desired schema...");
        var desiredSchema = await desiredProvider.GetSchema(options.Value.SchemaNames, cancellationToken);
        var schemasInScope = options.Value.SchemaNames ?? desiredSchema.AllSchemaNames;

        reporter.Info("Validating schema...");
        var schemaDiagnostics = schemaPolicies.SelectMany(p => p.Validate(desiredSchema));
        var diagnostics = new PolicyDiagnostics(schemaDiagnostics);
        if (diagnostics.HasErrors)
        {
            reporter.ReportDiagnostics(diagnostics);
            throw new PolicyViolationException(diagnostics.Errors.ToList());
        }

        reporter.Info($"Migration will be scoped to the following schemas: {string.Join(", ", schemasInScope)}");

        reporter.Info("Loading provider schema...");
        var currentSchema = await currentProvider.GetSchema(currentSource, schemasInScope, required, cancellationToken);

        reporter.Info("Loading scripts...");
        var scriptLists = await Task.WhenAll(scriptProviders.Select(p => p.GetScripts(cancellationToken)));
        var scripts = scriptLists.SelectMany(s => s).ToList();

        reporter.Info("Computing migration plan...");
        var result = planner.Plan(currentSchema, desiredSchema, scripts);
        if (result.HasErrors)
        {
            reporter.ReportDiagnostics(result.Diagnostics);
            throw new PolicyViolationException(result.Diagnostics.Errors.ToList());
        }

        reporter.ReportDiff(result.Diff);
        reporter.ReportDiagnostics(result.Diagnostics);

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
