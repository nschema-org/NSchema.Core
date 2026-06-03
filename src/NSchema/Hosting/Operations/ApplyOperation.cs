using NSchema.Hosting.Services;
using NSchema.Migration;
using NSchema.Schema;
using NSchema.Sql;

namespace NSchema.Hosting.Operations;

internal sealed class ApplyOperation(
    IMigrationReporter reporter,
    IMigrationConfirmation confirmation,
    IMigrationHelper helper,
    ISqlGenerator? sqlGenerator = null,
    ISqlExecutor? sqlExecutor = null
) : IMigrationOperation
{
    public async Task Execute(CancellationToken cancellationToken = default)
    {
        if (sqlGenerator is null || sqlExecutor is null)
        {
            throw new InvalidOperationException("Applying a migration requires a database provider to generate and execute SQL, but none is registered.");
        }

        reporter.Info("Applying schema migration. Changes will be applied to the database.");

        var plan = await helper.Plan(SchemaSourceMode.Online, required: true, cancellationToken);

        reporter.Info("Generating SQL...");
        var sqlPlan = sqlGenerator.Generate(plan);
        reporter.ReportSqlPlan(sqlPlan);

        // Offer an interactive front-end the chance to prompt before any changes are made.
        if (!await confirmation.Confirm(plan, cancellationToken))
        {
            reporter.Info("Apply cancelled. No changes were made to the database.");
            return;
        }

        reporter.Info("Running database migration...");
        await sqlExecutor.Execute(sqlPlan, cancellationToken);
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
