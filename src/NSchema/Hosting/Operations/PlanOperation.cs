using Microsoft.Extensions.Options;
using NSchema.Migration;
using NSchema.Policies;

namespace NSchema.Hosting.Operations;

internal sealed class PlanOperation(
    IOptions<MigrationOptions> options,
    IMigrationPlanner planner,
    IMigrationReporter reporter,
    ICurrentSchemaProvider currentProvider,
    IDesiredSchemaProvider desiredProvider,
    IMigrationCompiler? compiler = null
) : IMigrationOperation
{
    public async Task Execute(CancellationToken cancellationToken = default)
    {
        reporter.Info("Running in Plan mode. No changes will be applied to the database.");

        reporter.Info("Computing migration plan...");
        var desiredSchema = await desiredProvider.GetSchema(options.Value.SchemaNames, cancellationToken);
        var schemasInScope = options.Value.SchemaNames ?? desiredSchema.AllSchemaNames;
        var currentSchema = await currentProvider.GetSchema(SchemaSourceMode.Offline, schemasInScope, required: false, cancellationToken);

        var result = await planner.Plan(currentSchema, desiredSchema, cancellationToken);

        if (result.HasErrors)
        {
            reporter.ReportDiagnostics(result.Diagnostics);
            throw new PolicyViolationException(result.Errors.ToList());
        }

        reporter.ReportPlan(result.Plan);
        reporter.ReportDiagnostics(result.Diagnostics);

        if (compiler is null)
        {
            reporter.Info("No SQL preview available — no database connection is configured.");
            return;
        }

        reporter.Info("Compiling migration plan...");
        var execution = await compiler.Compile(result.Plan, cancellationToken);
        if (execution.Preview.Count > 0)
        {
            reporter.Info("SQL Preview:");
            reporter.ReportPreview(execution.Preview);
        }
    }

}
