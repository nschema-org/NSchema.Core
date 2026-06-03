using NSchema.Hosting.Services;
using NSchema.Migration;
using NSchema.Migration.Sources;

namespace NSchema.Hosting.Operations;

internal sealed class ApplyOperation(
    IMigrationReporter reporter,
    IMigrationConfirmation confirmation,
    IMigrationHelper helper,
    IMigrationCompiler? compiler = null
) : IMigrationOperation
{
    public async Task Execute(CancellationToken cancellationToken = default)
    {
        if (compiler == null)
        {
            throw new InvalidOperationException("Applying a migration requires a database provider to compile the plan into SQL, but none is registered.");
        }

        reporter.Info("Applying schema migration. Changes will be applied to the database.");

        var plan = await helper.Prepare(SchemaSourceMode.Online, required: true, cancellationToken);

        reporter.Info("Compiling migration plan...");
        var execution = await compiler.Compile(plan, cancellationToken);
        reporter.ReportPreview(execution.Preview);

        // Offer an interactive front-end the chance to prompt before any changes are made.
        if (!await confirmation.Confirm(plan, cancellationToken))
        {
            reporter.Info("Apply cancelled. No changes were made to the database.");
            return;
        }

        reporter.Info("Running database migration...");
        await execution.Execute(cancellationToken);
        reporter.Info("Migration completed successfully.");

        // Capture the post-apply state only when a store is configured; otherwise there's nowhere to write it.
        if (helper.HasStore)
        {
            reporter.Info("Updating state store...");
            await helper.Refresh(cancellationToken);
            reporter.Info("State store updated successfully.");
        }
    }
}
