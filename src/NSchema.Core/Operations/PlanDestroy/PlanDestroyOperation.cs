using NSchema.Operations.Services;
using NSchema.Plan.PlanFile;
using NSchema.Resolution;
using NSchema.Sql;

namespace NSchema.Operations.PlanDestroy;

internal sealed class PlanDestroyOperation(
    IKeyedResolver<IOperationReporter> reporters,
    IMigrationWorkflow workflow,
    IKeyedResolver<ISqlGenerator> sqlGenerator,
    IPlanFileWriter handler
) : IPlanDestroyOperation
{
    public async Task Execute(PlanDestroyArguments arguments, CancellationToken cancellationToken = default)
    {
        reporters.Current.Announce("Planning schema teardown. No changes will be applied to the database.");

        // Same trusted teardown path Destroy uses (bypasses the diff/plan transformers and policies); we just
        // preview it instead of executing, so there is no confirmation and no state capture.
        var planned = await workflow.PlanDestroy(cancellationToken);
        if (!sqlGenerator.HasCurrent)
        {
            if (arguments.OutFile is not null)
            {
                throw new InvalidOperationException("Saving a plan to a file requires a database provider to generate SQL, but none is registered.");
            }

            reporters.Current.Warn("Unable to generate SQL preview. No provider is configured.");
            return;
        }

        var sqlPlan = sqlGenerator.Current.Generate(planned.Plan);
        reporters.Current.ReportSqlPlan(sqlPlan);

        if (arguments.OutFile is not null)
        {
            var envelope = new PlanFileEnvelope(planned.Plan, sqlPlan, planned.Diff, DateTimeOffset.UtcNow);
            await handler.Write(arguments.OutFile, envelope, cancellationToken);
            reporters.Current.Success($"Planned destroy saved to {arguments.OutFile}. Apply it later with this file to execute exactly this plan.");
        }
    }
}
