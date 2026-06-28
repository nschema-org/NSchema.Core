using NSchema.Diagnostics;
using NSchema.Operations.Plan;
using NSchema.Operations.Services;
using NSchema.Plan.PlanFile;
using NSchema.Sql;

namespace NSchema.Operations.PlanDestroy;

internal sealed class PlanDestroyOperation(
    IOperationReporter reporter,
    IMigrationWorkflow workflow,
    IPlanFileWriter handler,
    ISqlGenerator? sqlGenerator = null
) : IPlanDestroyOperation
{
    public async Task<Result<PlanResult>> Execute(PlanDestroyArguments arguments, CancellationToken cancellationToken = default)
    {
        reporter.Announce("Planning schema teardown. No changes will be applied to the database.");

        // Same trusted teardown path Destroy uses (bypasses the diff/plan transformers and policies); we just
        // preview it instead of executing, so there is no confirmation and no state capture.
        var planned = await workflow.ComputeTeardown(cancellationToken);
        if (planned.Diff is not null)
        {
            reporter.ReportDiff(planned.Diff);
        }

        if (planned.HasErrors)
        {
            return Result<PlanResult>.Failure(planned.Diagnostics);
        }

        if (sqlGenerator is null)
        {
            if (arguments.OutFile is not null)
            {
                return Result<PlanResult>.Failure(
                    Diagnostic.Error("plan", "Saving a plan to a file requires a database provider to generate SQL, but none is registered."));
            }

            return Result<PlanResult>.Success(
                new PlanResult(planned.Diff),
                [.. planned.Diagnostics, Diagnostic.Warning("plan", "Unable to generate SQL preview. No provider is configured.")]);
        }

        var sqlPlan = sqlGenerator.Generate(planned.Plan);
        reporter.ReportSqlPlan(sqlPlan);

        if (arguments.OutFile is not null)
        {
            var envelope = new PlanFileEnvelope(planned.Plan, sqlPlan, planned.Diff, DateTimeOffset.UtcNow);
            await handler.Write(arguments.OutFile, envelope, cancellationToken);
            reporter.Success($"Planned destroy saved to {arguments.OutFile}. Apply it later with this file to execute exactly this plan.");
        }

        return Result<PlanResult>.Success(new PlanResult(planned.Diff), [.. planned.Diagnostics]);
    }
}
