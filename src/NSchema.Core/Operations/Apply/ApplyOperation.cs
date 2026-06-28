using NSchema.Diagnostics;
using NSchema.Operations.Confirmation;
using NSchema.Operations.Progress;
using NSchema.Operations.Services;
using NSchema.Plan.PlanFile;
using NSchema.Schema;
using NSchema.Sql;
using NSchema.State;

namespace NSchema.Operations.Apply;

internal sealed class ApplyOperation(
    IOperationReporter reporter,
    IProgress<OperationProgress> progress,
    IOperationConfirmation confirmation,
    IMigrationWorkflow workflow,
    IPlanFileWriter planFile,
    IStateLock? stateLock = null,
    ISqlGenerator? sqlGenerator = null,
    ISqlExecutor? sqlExecutor = null
) : IApplyOperation
{
    public async Task<Result> Execute(ApplyArguments arguments, CancellationToken cancellationToken = default)
    {
        if (sqlGenerator is null || sqlExecutor is null)
        {
            return Result.Failure(Diagnostic.Error("apply", "Applying a migration requires a database provider to generate and execute SQL, but none is registered."));
        }

        if (arguments.PlanFile is not null)
        {
            return await ApplyFromFile(arguments.PlanFile, sqlExecutor, arguments.SkipLock, cancellationToken);
        }

        reporter.Announce("Applying schema migration. Changes will be applied to the database.");

        // Hold the state lock for the whole operation so a concurrent apply/destroy/refresh can't run against the same state.
        var stateLockHandle = await StateLockGuard.AcquireOrSkip(stateLock, reporter, "apply", arguments.SkipLock, cancellationToken);
        try
        {
            var planned = await workflow.ComputePlan(SchemaSourceMode.Online, required: true, arguments.Schemas, cancellationToken);

            // Show the diff first — even on a policy error — so the offending change is visible.
            if (planned.Diff is not null)
            {
                reporter.ReportDiff(planned.Diff);
            }

            // A blocked policy is carried back as a failed result rather than thrown.
            if (planned.HasErrors)
            {
                return Result.Failure(planned.Diagnostics);
            }

            reporter.ReportPlan(planned.Plan);

            progress.Report(OperationProgress.Step("Generating SQL..."));
            var sqlPlan = sqlGenerator.Generate(planned.Plan);

            // The database already matches the desired schema: there's nothing to confirm or execute. Still capture
            // state, so a first run against an already-matching database initialises the store.
            if (sqlPlan.IsEmpty)
            {
                reporter.Success("No changes. The database already matches the desired schema.");
                await workflow.Refresh(RefreshMode.Optional, cancellationToken);
                return Result.Success([.. planned.Diagnostics]);
            }

            reporter.ReportSqlPlan(sqlPlan);

            // Offer an interactive front-end the chance to prompt before any changes are made.
            if (!await confirmation.Confirm(new ApplyConfirmationRequest(planned.Plan), cancellationToken))
            {
                reporter.Announce("Apply cancelled. No changes were made to the database.");
                return Result.Success([.. planned.Diagnostics]);
            }

            progress.Report(OperationProgress.Step("Running schema migration..."));
            progress.Report(OperationProgress.Detail(RunSummary.DescribeExecution(sqlPlan)));

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

            return Result.Success([.. planned.Diagnostics]);
        }
        finally
        {
            // Release with an uncancellable token so a cancelled apply still frees its own lock.
            if (stateLockHandle is not null)
            {
                await stateLockHandle.Release(CancellationToken.None);
            }
        }
    }

    /// <summary>
    /// Applies a previously saved plan file.
    /// </summary>
    private async Task<Result> ApplyFromFile(string path, ISqlExecutor sqlExecutor, bool skipLock, CancellationToken cancellationToken)
    {
        var envelope = await planFile.Read(path, cancellationToken);

        reporter.Announce($"Applying saved plan from {path}. Changes will be applied to the database.");
        progress.Report(OperationProgress.Detail($"Saved plan was created at {envelope.CreatedAt:u}."));

        var stateLockHandle = await StateLockGuard.AcquireOrSkip(stateLock, reporter, "apply", skipLock, cancellationToken);
        try
        {
            // Report the same diff/plan/SQL view the plan step produced, so applying a saved plan looks identical.
            reporter.ReportDiff(envelope.Diff);
            reporter.ReportPlan(envelope.Plan);
            reporter.ReportSqlPlan(envelope.Sql);

            if (!await confirmation.Confirm(new ApplyConfirmationRequest(envelope.Plan), cancellationToken))
            {
                reporter.Announce("Apply cancelled. No changes were made to the database.");
                return Result.Success();
            }

            progress.Report(OperationProgress.Step("Applying plan..."));
            progress.Report(OperationProgress.Detail(RunSummary.DescribeExecution(envelope.Sql)));

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

            return Result.Success();
        }
        finally
        {
            if (stateLockHandle is not null)
            {
                await stateLockHandle.Release(CancellationToken.None);
            }
        }
    }
}
