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

        reporter.Info("Computing migration plan...");
        var desiredSchema = await desiredProvider.GetSchema(options.Value.SchemaNames, cancellationToken);
        var schemasInScope = options.Value.SchemaNames ?? desiredSchema.AllSchemaNames;
        var currentSchema = await currentProvider.GetSchema(SchemaSourceMode.Online, schemasInScope, required: true, cancellationToken);

        var result = await planner.Plan(currentSchema, desiredSchema, cancellationToken);

        reporter.ReportDiagnostics(result.Diagnostics);
        if (result.HasErrors)
        {
            throw new PolicyViolationException(result.Errors.ToList());
        }

        reporter.ReportPlan(result.Plan);

        reporter.Info("Compiling migration plan...");
        var execution = await compiler.Compile(result.Plan, cancellationToken);
        reporter.ReportPreview(execution.Preview);

        reporter.Info("Running database migration...");
        await execution.Execute(cancellationToken);
        reporter.Info("Migration completed successfully.");

        // Capture the resulting schema so a later offline plan can diff against it.
        if (store is not null)
        {
            reporter.Info("Capturing schema state...");
            var snapshot = await currentProvider.GetSchema(SchemaSourceMode.Online, options.Value.SchemaNames, required: true, cancellationToken);
            await store.Write(snapshot, cancellationToken);
            reporter.Info("Schema state captured.");
        }
    }
}
