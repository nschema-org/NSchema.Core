using Microsoft.Extensions.Options;
using NSchema.Migration;
using NSchema.Policies;

namespace NSchema.Hosting.Operations;

internal sealed class PlanOperation(
    IMigrationPlanner planner,
    IMigrationReporter reporter,
    ICurrentSchemaProvider currentProvider,
    IDesiredSchemaProvider desiredProvider,
    IOptions<MigrationOptions> options,
    IMigrationCompiler? compiler = null
) : IMigrationOperation
{
    public async Task Execute(CancellationToken cancellationToken = default)
    {
        reporter.Info("Running in Plan mode. No changes will be applied to the database.");

        reporter.Info("Computing migration plan...");
        var source = currentProvider.GetSource(SchemaSourceMode.Offline, required: false);
        var (currentSchema, desiredSchema) = await SchemaResolution.ResolveAsync(source, desiredProvider, options.Value.SchemaNames, cancellationToken);

        var result = await planner.Plan(currentSchema, desiredSchema, cancellationToken);

        reporter.ReportDiagnostics(result.Diagnostics);
        if (result.HasErrors)
        {
            throw new PolicyViolationException(result.Errors.ToList());
        }

        reporter.ReportPlan(result.Plan);

        if (compiler is null)
        {
            reporter.Info("No database provider registered; reporting the plan without a SQL preview.");
            return;
        }

        reporter.Info("Compiling migration plan...");
        var execution = await compiler.Compile(result.Plan, cancellationToken);
        reporter.ReportPreview(execution.Preview);
    }

}
