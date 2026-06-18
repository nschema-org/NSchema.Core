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
    public async Task Execute(PlanDestroyArguments arguments, CancellationToken cancellationToken = default)
    {
        reporter.Announce("Planning schema teardown. No changes will be applied to the database.");

        // Same trusted teardown path Destroy uses (bypasses the diff/plan transformers and policies); we just
        // preview it instead of executing, so there is no confirmation and no state capture.
        var planned = await workflow.PlanDestroy(cancellationToken);
        if (sqlGenerator is null)
        {
            if (arguments.OutFile is not null)
            {
                throw new InvalidOperationException("Saving a plan to a file requires a database provider to generate SQL, but none is registered.");
            }

            reporter.Warn("Unable to generate SQL preview. No provider is configured.");
            return;
        }

        var sqlPlan = sqlGenerator.Generate(planned.Plan);
        reporter.ReportSqlPlan(sqlPlan);

        if (arguments.OutFile is not null)
        {
            var envelope = new PlanFileEnvelope(planned.Plan, sqlPlan, planned.Diff, DateTimeOffset.UtcNow);
            await handler.Write(arguments.OutFile, envelope, cancellationToken);
            reporter.Success($"Planned destroy saved to {arguments.OutFile}. Apply it later with this file to execute exactly this plan.");
        }
    }
}
