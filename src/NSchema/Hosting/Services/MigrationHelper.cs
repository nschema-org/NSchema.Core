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
    IMigrationReporterResolver reporter,
    ICurrentSchemaProvider currentProvider,
    IDesiredSchemaProvider desiredProvider,
    IEnumerable<ISchemaPolicy> schemaPolicies,
    ISchemaStateStore? store = null
) : IMigrationHelper
{
    public bool HasStore => store is not null;

    public async Task<MigrationPlan> Plan(SchemaSourceMode currentSource, bool required, CancellationToken cancellationToken = default)
    {
        reporter.Current.Info("Loading desired schema...");
        var desiredSchema = await desiredProvider.GetSchema(options.Value.SchemaNames, cancellationToken);
        var schemasInScope = options.Value.SchemaNames ?? desiredSchema.AllSchemaNames;

        reporter.Current.Info("Validating schema...");
        var schemaDiagnostics = new PolicyDiagnostics(schemaPolicies.SelectMany(p => p.Validate(desiredSchema)));
        if (schemaDiagnostics.Count > 0)
        {
            reporter.Current.ReportDiagnostics(schemaDiagnostics);
        }

        if (schemaDiagnostics.HasErrors)
        {
            throw new PolicyViolationException(schemaDiagnostics.Errors.ToList());
        }

        reporter.Current.Info($"Migration will be scoped to the following schemas: {string.Join(", ", schemasInScope)}");

        reporter.Current.Info("Loading provider schema...");
        var currentSchema = await currentProvider.GetSchema(currentSource, schemasInScope, required, cancellationToken);

        reporter.Current.Info("Loading scripts...");
        var scriptLists = await Task.WhenAll(scriptProviders.Select(p => p.GetScripts(cancellationToken)));
        var scripts = scriptLists.SelectMany(s => s).ToList();

        reporter.Current.Info("Computing migration plan...");
        var result = planner.Plan(currentSchema, desiredSchema, scripts);
        if (result.HasErrors)
        {
            reporter.Current.ReportDiagnostics(result.Diagnostics);
            throw new PolicyViolationException(result.Diagnostics.Errors.ToList());
        }

        reporter.Current.ReportDiff(result.Diff);
        reporter.Current.ReportDiagnostics(result.Diagnostics);

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
