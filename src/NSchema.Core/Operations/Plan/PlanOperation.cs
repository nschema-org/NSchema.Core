using NSchema.Diagnostics;
using NSchema.Operations.Services;
using NSchema.Plan.PlanFile;
using NSchema.Schema;
using NSchema.Sql;

namespace NSchema.Operations.Plan;

internal sealed class PlanOperation(
    IOperationReporter reporter,
    IMigrationWorkflow workflow,
    IPlanFileWriter handler,
    ISqlGenerator? sqlGenerator = null
) : IPlanOperation
{
    public async Task<Result<PlanResult>> Execute(PlanArguments arguments, CancellationToken cancellationToken = default)
    {
        reporter.Announce("Planning schema migration. No changes will be applied to the database.");

        var planned = await workflow.ComputePlan(SchemaSourceMode.Offline, required: false, arguments.Schemas, cancellationToken);

        // Show the diff first — even on a policy error — so the offending change is visible.
        if (planned.Diff is not null)
        {
            reporter.ReportDiff(planned.Diff);
        }

        // A policy error (e.g. a blocked destructive change) is carried back as a failed result, not thrown.
        if (planned.HasErrors)
        {
            return Result<PlanResult>.Failure(planned.Diagnostics);
        }

        if (sqlGenerator is null)
        {
            // Without a provider there is no SQL to preview — and no way to write a saved plan.
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
            reporter.Success($"Plan saved to {arguments.OutFile}. Apply it later with this file to execute exactly this plan.");
        }

        return Result<PlanResult>.Success(new PlanResult(planned.Diff), [.. planned.Diagnostics]);
    }
}
