using NSchema.Operations.Confirmation;
using NSchema.Operations.Services;
using NSchema.Sql;
using NSchema.State;

using NSchema.Operations.Progress;

namespace NSchema.Operations.Destroy;

internal sealed class DestroyOperation(
    IOperationReporter reporter,
    IProgress<OperationProgress> progress,
    IOperationConfirmation confirmation,
    IMigrationWorkflow workflow,
    IStateLock? stateLock = null,
    ISqlGenerator? sqlGenerator = null,
    ISqlExecutor? sqlExecutor = null
) : IDestroyOperation
{
    public async Task Execute(DestroyArguments arguments, CancellationToken cancellationToken = default)
    {
        if (sqlGenerator is null || sqlExecutor is null)
        {
            throw new InvalidOperationException("Destroying a schema requires a database provider to generate and execute SQL, but none is registered.");
        }

        reporter.Announce("Destroying schema. All managed objects will be dropped from the database.");

        // Hold the state lock for the whole teardown so a concurrent apply/destroy/refresh can't run against the same state.
        var stateLockHandle = await StateLockGuard.AcquireOrSkip(stateLock, reporter, "destroy", arguments.SkipLock, cancellationToken);
        try
        {
            var planned = await workflow.PlanDestroy(cancellationToken);

            progress.Report(OperationProgress.Step("Generating SQL..."));
            var sqlPlan = sqlGenerator.Generate(planned.Plan);
            reporter.ReportSqlPlan(sqlPlan);

            // Offer an interactive front-end the chance to prompt before any changes are made.
            if (!await confirmation.Confirm(new DestroyConfirmationRequest(planned.Plan), cancellationToken))
            {
                reporter.Announce("Destroy cancelled. No changes were made to the database.");
                return;
            }

            progress.Report(OperationProgress.Step("Running schema teardown..."));
            progress.Report(OperationProgress.Detail(RunSummary.DescribeExecution(sqlPlan)));

            try
            {
                await sqlExecutor.Execute(sqlPlan, cancellationToken);
                reporter.Success($"Destroy complete. {RunSummary.Describe(planned.Diff, sqlPlan)}.");
            }
            finally
            {
                // Capture the resulting state when a store is configured; a no-op otherwise. This runs even when
                // teardown failed partway (e.g. an un-transacted plan) so the store reflects what was actually dropped.
                await workflow.Refresh(RefreshMode.Optional, cancellationToken);
            }
        }
        finally
        {
            // Release with an uncancellable token so a cancelled teardown still frees its own lock.
            if (stateLockHandle is not null)
            {
                await stateLockHandle.Release(CancellationToken.None);
            }
        }
    }
}
