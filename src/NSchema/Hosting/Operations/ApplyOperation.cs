using Microsoft.Extensions.Options;
using NSchema.Migration;
using NSchema.Policies;
using NSchema.State;

namespace NSchema.Hosting.Operations;

internal sealed class ApplyOperation(
    IOptions<MigrationOptions> options,
    IMigrationPlanner planner,
    IMigrationReporter reporter,
    ICurrentSchemaProvider currentProvider,
    IDesiredSchemaProvider desiredProvider,
    IMigrationConfirmation confirmation,
    ISchemaStateStore? store = null,
    IMigrationCompiler? compiler = null
) : IMigrationOperation
{
    public async Task Execute(CancellationToken cancellationToken = default)
    {
        if (compiler is null)
        {
            throw new InvalidOperationException("Applying a migration requires a database provider to compile the plan into SQL, but none is registered.");
        }

        reporter.Info("Running in Apply mode. Changes will be applied to the database.");

        reporter.Info("Computing migration plan...");
        var desiredSchema = await desiredProvider.GetSchema(options.Value.SchemaNames, cancellationToken);
        var schemasInScope = options.Value.SchemaNames ?? desiredSchema.AllSchemaNames;
        var currentSchema = await currentProvider.GetSchema(SchemaSourceMode.Online, schemasInScope, required: true, cancellationToken);

        var result = await planner.Plan(currentSchema, desiredSchema, cancellationToken);

        if (result.HasErrors)
        {
            reporter.ReportDiagnostics(result.Diagnostics);
            throw new PolicyViolationException(result.Errors.ToList());
        }

        reporter.ReportPlan(result.Plan);
        reporter.ReportDiagnostics(result.Diagnostics);

        reporter.Info("Compiling migration plan...");
        var execution = await compiler.Compile(result.Plan, cancellationToken);
        if (execution.Preview.Count > 0)
        {
            reporter.Info("SQL Preview:");
            reporter.ReportPreview(execution.Preview);
        }

        // Offer an interactive front-end the chance to prompt before any changes are made.
        if (!await confirmation.Confirm(result.Plan, cancellationToken))
        {
            reporter.Info("Apply cancelled. No changes were made to the database.");
            return;
        }

        reporter.Info("Running database migration...");
        await execution.Execute(cancellationToken);
        reporter.Info("Migration completed successfully.");

        // Capture the resulting schema so a later offline plan can diff against it.
        if (store is not null)
        {
            reporter.Info("Capturing schema state...");
            var snapshot = await currentProvider.GetSchema(SchemaSourceMode.Online, null, required: true, cancellationToken);
            await store.Write(snapshot, cancellationToken);
            reporter.Info("Schema state captured.");
        }
    }
}
