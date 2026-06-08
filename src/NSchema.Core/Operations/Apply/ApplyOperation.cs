using NSchema.Operations.Confirmation;
using NSchema.Operations.Services;
using NSchema.Resolution;
using NSchema.Schema;
using NSchema.Sql;

namespace NSchema.Operations.Apply;

internal sealed class ApplyOperation(
    IKeyedResolver<IOperationReporter> reporters,
    IOperationConfirmation confirmation,
    IMigrationHelper helper,
    IKeyedResolver<ISqlGenerator> sqlGenerators,
    ISqlExecutor? sqlExecutor = null
) : IApplyOperation
{
    public async Task Execute(ApplyArguments arguments, CancellationToken cancellationToken = default)
    {
        if (!sqlGenerators.HasCurrent || sqlExecutor is null)
        {
            throw new InvalidOperationException("Applying a migration requires a database provider to generate and execute SQL, but none is registered.");
        }

        reporters.Current.Info("Applying schema migration. Changes will be applied to the database.");

        var plan = await helper.Plan(SchemaSourceMode.Online, required: true, arguments.Schemas, cancellationToken);

        reporters.Current.Info("Generating SQL...");
        var sqlPlan = sqlGenerators.Current.Generate(plan);
        reporters.Current.ReportSqlPlan(sqlPlan);

        // Offer an interactive front-end the chance to prompt before any changes are made.
        if (!await confirmation.Confirm(new ApplyConfirmationRequest(plan), cancellationToken))
        {
            reporters.Current.Info("Apply cancelled. No changes were made to the database.");
            return;
        }

        reporters.Current.Info("Running schema migration...");
        await sqlExecutor.Execute(sqlPlan, cancellationToken);
        reporters.Current.Info("Migration completed successfully.");

        // Capture the post-apply state only when a store is configured; otherwise there's nowhere to write it.
        if (helper.HasStore)
        {
            reporters.Current.Info("Updating state store...");
            await helper.Refresh(cancellationToken);
            reporters.Current.Info("State store updated successfully.");
        }
    }
}
