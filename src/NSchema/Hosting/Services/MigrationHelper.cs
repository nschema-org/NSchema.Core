using Microsoft.Extensions.Options;
using NSchema.Migration;
using NSchema.Migration.Plan;
using NSchema.Policies;
using NSchema.State;

namespace NSchema.Hosting.Services;

internal class MigrationHelper(
    IOptions<MigrationOptions> options,
    IMigrationPlanner planner,
    IMigrationReporter reporter,
    ICurrentSchemaProvider currentProvider,
    IDesiredSchemaProvider desiredProvider,
    ISchemaStateStore? store = null
) : IMigrationHelper
{
    public bool HasStore => store is not null;

    public async Task<MigrationPlan> Prepare(CancellationToken cancellationToken = default)
    {
        reporter.Info("Loading desired schema...");
        var desiredSchema = await desiredProvider.GetSchema(options.Value.SchemaNames, cancellationToken);
        var schemasInScope = options.Value.SchemaNames ?? desiredSchema.AllSchemaNames;

        reporter.Info($"Migration plan will be scoped to the following schemas: {string.Join(", ", schemasInScope)}");

        reporter.Info("Loading provider schema...");
        var currentSchema = await currentProvider.GetSchema(SchemaSourceMode.Offline, schemasInScope, required: false, cancellationToken);

        reporter.Info("Computing migration plan...");
        var result = await planner.Plan(currentSchema, desiredSchema, cancellationToken);
        if (result.HasErrors)
        {
            reporter.ReportDiagnostics(result.Diagnostics);
            throw new PolicyViolationException(result.Errors.ToList());
        }

        reporter.ReportPlan(result.Plan);
        reporter.ReportDiagnostics(result.Diagnostics);

        return result.Plan;
    }

    public async Task Refresh(CancellationToken cancellationToken = default)
    {
        if (store == null)
        {
            throw new InvalidOperationException("Unable to refresh state without backend store.");
        }

        reporter.Info("Refreshing schema state...");
        var schema = await currentProvider.GetSchema(SchemaSourceMode.Online, null, required: true, cancellationToken);
        await store.Write(schema, cancellationToken);
        reporter.Info("Schema state refreshed successfully.");
    }
}
