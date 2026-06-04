using NSchema.Hosting.Services;
using NSchema.Schema;
using NSchema.Sql;

namespace NSchema.Hosting.Operations;

internal sealed class ApplyOperation(
    IMigrationReporterResolver reporter,
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

        reporter.Current.Info("Applying schema migration. Changes will be applied to the database.");

        var plan = await helper.Plan(SchemaSourceMode.Online, required: true, cancellationToken);

        reporter.Current.Info("Generating SQL...");
        var sqlPlan = sqlGenerator.Generate(plan);
        reporter.Current.ReportSqlPlan(sqlPlan);

        // Offer an interactive front-end the chance to prompt before any changes are made.
        if (!await confirmation.Confirm(plan, cancellationToken))
        {
            reporter.Current.Info("Apply cancelled. No changes were made to the database.");
            return;
        }

        reporter.Current.Info("Running database migration...");
        await sqlExecutor.Execute(sqlPlan, cancellationToken);
        reporter.Current.Info("Migration completed successfully.");

        // Capture the post-apply state only when a store is configured; otherwise there's nowhere to write it.
        if (helper.HasStore)
        {
            reporter.Current.Info("Updating state store...");
            await helper.Refresh(cancellationToken);
            reporter.Current.Info("State store updated successfully.");
        }
    }
}
