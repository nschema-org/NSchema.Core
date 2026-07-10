using NSchema.Diagnostics;
using NSchema.Operations.Progress;
using NSchema.Operations.Services;
using NSchema.Sql;
using NSchema.Sql.Model;

namespace NSchema.Operations.Apply;

/// <summary>
/// Applies a computed plan.
/// </summary>
internal sealed class ApplyOperation(IMigrationWorkflow workflow, IProgress<OperationProgress> progress, ISqlExecutor? sqlExecutor = null)
    : IOperation<ApplyArguments, Result<ApplyResult>>
{
    public async Task<Result<ApplyResult>> Execute(ApplyArguments args, CancellationToken cancellationToken = default)
    {
        // An empty plan executes nothing, but still records state.
        if (args.Sql.IsEmpty)
        {
            var emptyCapture = await workflow.Refresh(null, force: true, cancellationToken);
            return Result.Success(new ApplyResult(args.Sql), emptyCapture?.Diagnostics ?? []);
        }

        if (sqlExecutor is null)
        {
            return Result.Failure<ApplyResult>(Diagnostic.Error("apply", "Applying a plan requires a database provider to execute SQL, but none is registered."));
        }

        progress.Report(OperationProgress.Step("Applying the migration plan..."));
        progress.Report(OperationProgress.Detail(DescribeExecution(args.Sql)));

        try
        {
            await sqlExecutor.Execute(args.Sql, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The migration failed, possibly partway. Best-effort capture so the store reflects reality.
            // The plan is NOT passed as applied: we don't know which scripts ran.
            try
            {
                await workflow.Refresh(null, force: true, cancellationToken);
            }
            catch (Exception captureFailure) when (captureFailure is not OperationCanceledException)
            {
                // Swallow this so we don't mask the original error.
            }

            throw;
        }

        // Recording after execution must not refuse an unreadable payload — the SQL has already run, so the
        // store is force-updated to reflect reality and the reset ledger is surfaced as a warning instead.
        var capture = await workflow.Refresh(args.Sql, force: true, cancellationToken);

        return Result.Success(new ApplyResult(args.Sql), capture?.Diagnostics ?? []);
    }

    // A verbose progress line: the statement count and how many must run outside a transaction (e.g.
    // CREATE INDEX CONCURRENTLY) — the atomicity-breakers worth knowing about when diagnosing a partial apply.
    private static string DescribeExecution(SqlPlan sql)
    {
        var count = sql.Statements.Count;
        var statements = count == 1 ? "1 statement" : $"{count} statements";
        var outside = sql.Statements.Count(s => s.RunOutsideTransaction);

        return outside == 0
            ? $"Executing {statements}."
            : $"Executing {statements} ({outside} must run outside a transaction).";
    }
}
