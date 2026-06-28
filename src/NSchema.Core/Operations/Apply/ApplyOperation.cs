using NSchema.Diagnostics;
using NSchema.Operations.Plan;
using NSchema.Operations.Progress;
using NSchema.Operations.Services;
using NSchema.Sql;
using NSchema.Sql.Model;

namespace NSchema.Operations.Apply;

/// <summary>
/// Applies a computed plan.
/// </summary>
internal sealed class ApplyOperation(IMigrationWorkflow workflow, IProgress<OperationProgress> progress, ISqlExecutor? sqlExecutor = null)
    : IOperation<PlanResult, Result>
{
    public async Task<Result> Execute(PlanResult plan, CancellationToken cancellationToken = default)
    {
        // No SQL to run — either the target already matches or no provider generated a plan. Still capture state so a
        // first run against an already-matching target initialises the store.
        if (plan.Sql is null or { IsEmpty: true })
        {
            await workflow.Refresh(RefreshMode.Optional, cancellationToken);
            return Result.Success();
        }

        if (sqlExecutor is null)
        {
            return Result.Failure(Diagnostic.Error("apply", "Applying a plan requires a database provider to execute SQL, but none is registered."));
        }

        progress.Report(OperationProgress.Step("Applying the migration plan..."));
        progress.Report(OperationProgress.Detail(DescribeExecution(plan.Sql)));

        try
        {
            await sqlExecutor.Execute(plan.Sql, cancellationToken);
        }
        finally
        {
            // Capture the resulting state when a store is configured; a no-op otherwise. This runs even when execution
            // failed partway (e.g. an un-transacted plan) so the store reflects what was actually applied.
            await workflow.Refresh(RefreshMode.Optional, cancellationToken);
        }

        return Result.Success();
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
