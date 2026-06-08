using NSchema.Operations.Confirmation;
using NSchema.Operations.Services;
using NSchema.Resolution;
using NSchema.Schema;
using NSchema.Sql;
using NSchema.State;

namespace NSchema.Operations.Apply;

internal sealed class ApplyOperation(
    IKeyedResolver<IOperationReporter> reporters,
    IOperationConfirmation confirmation,
    IMigrationWorkflow workflow,
    IKeyedResolver<ISqlGenerator> sqlGenerators,
    IStateLock stateLock,
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

        // Hold the state lock for the whole operation so a concurrent apply/destroy/refresh can't run against the
        // same state. Released when the handle is disposed at the end of the method (no-op unless a lock is registered).
        await using var stateLockHandle = await stateLock.Acquire(new StateLockRequest("apply"), cancellationToken);

        var plan = await workflow.Plan(SchemaSourceMode.Online, required: true, arguments.Schemas, cancellationToken);

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

        try
        {
            await sqlExecutor.Execute(sqlPlan, cancellationToken);
            reporters.Current.Info("Migration completed successfully.");
        }
        finally
        {
            // Capture the resulting state when a store is configured; a no-op otherwise. This runs even when
            // execution failed partway (e.g. an un-transacted plan) so the store reflects what was actually applied.
            await workflow.Refresh(RefreshMode.Optional, cancellationToken);
        }
    }
}
