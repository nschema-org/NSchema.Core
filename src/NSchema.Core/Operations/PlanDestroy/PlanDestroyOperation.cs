using NSchema.Operations.Services;
using NSchema.Resolution;
using NSchema.Sql;

namespace NSchema.Operations.PlanDestroy;

internal sealed class PlanDestroyOperation(
    IKeyedResolver<IOperationReporter> reporters,
    IMigrationWorkflow workflow,
    IKeyedResolver<ISqlGenerator> sqlGenerator
) : IPlanDestroyOperation
{
    public async Task Execute(PlanDestroyArguments arguments, CancellationToken cancellationToken = default)
    {
        reporters.Current.Info("Planning schema teardown. No changes will be applied to the database.");

        // Same trusted teardown path Destroy uses (bypasses the diff/plan transformers and policies); we just
        // preview it instead of executing, so there is no confirmation and no state capture.
        var plan = await workflow.PlanDestroy(cancellationToken);
        if (!sqlGenerator.HasCurrent)
        {
            reporters.Current.Info("Unable to generate SQL preview. No provider is configured.");
            return;
        }

        var sqlPlan = sqlGenerator.Current.Generate(plan);
        reporters.Current.ReportSqlPlan(sqlPlan);
    }
}
