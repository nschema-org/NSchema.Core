using NSchema.Operations.Confirmation;
using NSchema.Operations.Services;
using NSchema.Resolution;
using NSchema.Sql;
using NSchema.State;
using NSchema.State.Model;

namespace NSchema.Operations.Destroy;

internal sealed class DestroyOperation(
    IOperationReporter reporter,
    IOperationConfirmation confirmation,
    IMigrationWorkflow workflow,
    IKeyedResolver<ISqlGenerator> sqlGenerators,
    IStateLock stateLock,
    ISqlExecutor? sqlExecutor = null
) : IDestroyOperation
{
    public async Task Execute(DestroyArguments arguments, CancellationToken cancellationToken = default)
    {
        if (!sqlGenerators.HasCurrent || sqlExecutor is null)
        {
            throw new InvalidOperationException("Destroying a schema requires a database provider to generate and execute SQL, but none is registered.");
        }

        reporter.Announce("Destroying schema. All managed objects will be dropped from the database.");

        // Hold the state lock for the whole teardown so a concurrent apply/destroy/refresh can't run against the
        // same state. Released when the handle is disposed at the end of the method (no-op unless a lock is registered).
        await using var stateLockHandle = await stateLock.Acquire(new StateLockRequest("destroy"), cancellationToken);

        var planned = await workflow.PlanDestroy(cancellationToken);

        reporter.Progress("Generating SQL...");
        var sqlPlan = sqlGenerators.Current.Generate(planned.Plan);
        reporter.ReportSqlPlan(sqlPlan);

        // Offer an interactive front-end the chance to prompt before any changes are made.
        if (!await confirmation.Confirm(new DestroyConfirmationRequest(planned.Plan), cancellationToken))
        {
            reporter.Announce("Destroy cancelled. No changes were made to the database.");
            return;
        }

        reporter.Progress("Running schema teardown...");

        try
        {
            await sqlExecutor.Execute(sqlPlan, cancellationToken);
            reporter.Success("Schema destroyed successfully.");
        }
        finally
        {
            // Capture the resulting state when a store is configured; a no-op otherwise. This runs even when
            // teardown failed partway (e.g. an un-transacted plan) so the store reflects what was actually dropped.
            await workflow.Refresh(RefreshMode.Optional, cancellationToken);
        }
    }
}
