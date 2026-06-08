using NSchema.Operations.Confirmation;
using NSchema.Operations.Services;
using NSchema.Resolution;
using NSchema.Sql;

namespace NSchema.Operations.Destroy;

internal sealed class DestroyOperation(
    IKeyedResolver<IOperationReporter> reporters,
    IOperationConfirmation confirmation,
    IMigrationWorkflow workflow,
    IKeyedResolver<ISqlGenerator> sqlGenerators,
    ISqlExecutor? sqlExecutor = null
) : IDestroyOperation
{
    public async Task Execute(DestroyArguments arguments, CancellationToken cancellationToken = default)
    {
        if (!sqlGenerators.HasCurrent || sqlExecutor is null)
        {
            throw new InvalidOperationException("Destroying a schema requires a database provider to generate and execute SQL, but none is registered.");
        }

        reporters.Current.Info("Destroying schema. All managed objects will be dropped from the database.");

        var plan = await workflow.PlanDestroy(cancellationToken);

        reporters.Current.Info("Generating SQL...");
        var sqlPlan = sqlGenerators.Current.Generate(plan);
        reporters.Current.ReportSqlPlan(sqlPlan);

        // Offer an interactive front-end the chance to prompt before any changes are made.
        if (!await confirmation.Confirm(new DestroyConfirmationRequest(plan), cancellationToken))
        {
            reporters.Current.Info("Destroy cancelled. No changes were made to the database.");
            return;
        }

        reporters.Current.Info("Running schema teardown...");

        try
        {
            await sqlExecutor.Execute(sqlPlan, cancellationToken);
            reporters.Current.Info("Schema destroyed successfully.");
        }
        finally
        {
            // Capture the resulting state when a store is configured; a no-op otherwise. This runs even when
            // teardown failed partway (e.g. an un-transacted plan) so the store reflects what was actually dropped.
            await workflow.Refresh(RefreshMode.Optional, cancellationToken);
        }
    }
}
