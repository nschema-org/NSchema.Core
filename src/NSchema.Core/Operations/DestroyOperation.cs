using NSchema.Operations.Services;
using NSchema.Resolution;
using NSchema.Sql;

namespace NSchema.Operations;

internal sealed class DestroyOperation(
    IKeyedResolver<IOperationReporter> reporters,
    IOperationConfirmation confirmation,
    IMigrationHelper helper,
    IKeyedResolver<ISqlGenerator> sqlGenerators,
    ISqlExecutor? sqlExecutor = null
) : IOperation
{
    public async Task Execute(CancellationToken cancellationToken = default)
    {
        if (!sqlGenerators.HasCurrent || sqlExecutor is null)
        {
            throw new InvalidOperationException("Destroying a schema requires a database provider to generate and execute SQL, but none is registered.");
        }

        reporters.Current.Info("Destroying schema. All managed objects will be dropped from the database.");

        var plan = await helper.PlanDestroy(cancellationToken);

        reporters.Current.Info("Generating SQL...");
        var sqlPlan = sqlGenerators.Current.Generate(plan);
        reporters.Current.ReportSqlPlan(sqlPlan);

        // Offer an interactive front-end the chance to prompt before any changes are made.
        if (!await confirmation.Confirm(plan, cancellationToken))
        {
            reporters.Current.Info("Destroy cancelled. No changes were made to the database.");
            return;
        }

        reporters.Current.Info("Running schema teardown...");
        await sqlExecutor.Execute(sqlPlan, cancellationToken);
        reporters.Current.Info("Schema destroyed successfully.");

        // Capture the post-destroy state only when a store is configured; otherwise there's nowhere to write it.
        if (helper.HasStore)
        {
            reporters.Current.Info("Updating state store...");
            await helper.Refresh(cancellationToken);
            reporters.Current.Info("State store updated successfully.");
        }
    }
}
