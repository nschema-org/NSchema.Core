using Microsoft.Extensions.Options;
using NSchema.Migration;
using NSchema.Migration.Diff;
using NSchema.Migration.Plan;
using NSchema.Migration.Sources;
using NSchema.Policies;
using NSchema.State;

namespace NSchema.Hosting.Services;

internal sealed class MigrationHelper(
    IOptions<MigrationOptions> options,
    IMigrationPlanner planner,
    IDiffBuilder diffBuilder,
    IMigrationReporter reporter,
    ICurrentSchemaProvider currentProvider,
    IDesiredSchemaProvider desiredProvider,
    ISchemaStateStore? store = null
) : IMigrationHelper
{
    public bool HasStore => store is not null;

    public async Task<MigrationPlan> Prepare(SchemaSourceMode currentSource, bool required, CancellationToken cancellationToken = default)
    {
        reporter.Info("Loading desired schema...");
        var desiredSchema = await desiredProvider.GetSchema(options.Value.SchemaNames, cancellationToken);
        var schemasInScope = options.Value.SchemaNames ?? desiredSchema.AllSchemaNames;

        reporter.Info($"Migration plan will be scoped to the following schemas: {string.Join(", ", schemasInScope)}");

        reporter.Info("Loading provider schema...");
        var currentSchema = await currentProvider.GetSchema(currentSource, schemasInScope, required, cancellationToken);

        reporter.Info("Computing migration plan...");
        var result = await planner.Plan(currentSchema, desiredSchema, cancellationToken);
        if (result.HasErrors)
        {
            reporter.ReportDiagnostics(result.Diagnostics);
            throw new PolicyViolationException(result.Errors.ToList());
        }

        var diff = diffBuilder.Build(result.Plan);
        reporter.ReportDiff(diff);
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
