using NSchema.Operations.Confirmation;
using NSchema.Operations.Services;
using NSchema.Plan.PlanFile;
using NSchema.Schema;
using NSchema.Sql;
using NSchema.State;
using NSchema.State.Model;

namespace NSchema.Operations.Apply;

internal sealed class ApplyOperation(
    IOperationReporter reporter,
    IOperationConfirmation confirmation,
    IMigrationWorkflow workflow,
    IStateLock stateLock,
    IPlanFileWriter planFile,
    ISqlGenerator? sqlGenerator = null,
    ISqlExecutor? sqlExecutor = null
) : IApplyOperation
{
    public async Task Execute(ApplyArguments arguments, CancellationToken cancellationToken = default)
    {
        if (sqlGenerator is null || sqlExecutor is null)
        {
            throw new InvalidOperationException("Applying a migration requires a database provider to generate and execute SQL, but none is registered.");
        }

        if (arguments.PlanFile is not null)
        {
            await ApplyFromFile(arguments.PlanFile, sqlExecutor, cancellationToken);
            return;
        }

        reporter.Announce("Applying schema migration. Changes will be applied to the database.");

        // Hold the state lock for the whole operation so a concurrent apply/destroy/refresh can't run against the
        // same state. Released when the handle is disposed at the end of the method (no-op unless a lock is registered).
        await using var stateLockHandle = await stateLock.Acquire(new StateLockRequest("apply"), cancellationToken);

        var planned = await workflow.Plan(SchemaSourceMode.Online, required: true, arguments.Schemas, cancellationToken);

        reporter.Progress("Generating SQL...");
        var sqlPlan = sqlGenerator.Generate(planned.Plan);

        // The database already matches the desired schema: there's nothing to confirm or execute. Still capture
        // state, so a first run against an already-matching database initialises the store.
        if (sqlPlan.IsEmpty)
        {
            reporter.Success("No changes. The database already matches the desired schema.");
            await workflow.Refresh(RefreshMode.Optional, cancellationToken);
            return;
        }

        reporter.ReportSqlPlan(sqlPlan);

        // Offer an interactive front-end the chance to prompt before any changes are made.
        if (!await confirmation.Confirm(new ApplyConfirmationRequest(planned.Plan), cancellationToken))
        {
            reporter.Announce("Apply cancelled. No changes were made to the database.");
            return;
        }

        reporter.Progress("Running schema migration...");

        try
        {
            await sqlExecutor.Execute(sqlPlan, cancellationToken);
            reporter.Success($"Apply complete. {RunSummary.Describe(planned.Diff, sqlPlan)}.");
        }
        finally
        {
            // Capture the resulting state when a store is configured; a no-op otherwise. This runs even when
            // execution failed partway (e.g. an un-transacted plan) so the store reflects what was actually applied.
            await workflow.Refresh(RefreshMode.Optional, cancellationToken);
        }
    }

    /// <summary>
    /// Applies a previously saved plan file.
    /// </summary>
    private async Task ApplyFromFile(string path, ISqlExecutor sqlExecutor, CancellationToken cancellationToken)
    {
        var envelope = await planFile.Read(path, cancellationToken);

        reporter.Announce($"Applying saved plan from {path}. Changes will be applied to the database.");

        await using var stateLockHandle = await stateLock.Acquire(new StateLockRequest("apply"), cancellationToken);

        // Report the same diff/plan/SQL view the plan step produced, so applying a saved plan looks identical.
        reporter.ReportDiff(envelope.Diff);
        reporter.ReportPlan(envelope.Plan);
        reporter.ReportSqlPlan(envelope.Sql);

        if (!await confirmation.Confirm(new ApplyConfirmationRequest(envelope.Plan), cancellationToken))
        {
            reporter.Announce("Apply cancelled. No changes were made to the database.");
            return;
        }

        reporter.Progress("Applying plan...");

        try
        {
            await sqlExecutor.Execute(envelope.Sql, cancellationToken);
            reporter.Success($"Apply complete. {RunSummary.Describe(envelope.Diff, envelope.Sql)}.");
        }
        finally
        {
            // Capture the resulting state when a store is configured; a no-op otherwise. This runs even when
            // execution failed partway (e.g. an un-transacted plan) so the store reflects what was actually applied.
            await workflow.Refresh(RefreshMode.Optional, cancellationToken);
        }
    }
}
