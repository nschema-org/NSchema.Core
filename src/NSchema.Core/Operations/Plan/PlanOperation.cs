using NSchema.Operations.Services;
using NSchema.Resolution;
using NSchema.Schema;
using NSchema.Sql;

namespace NSchema.Operations.Plan;

internal sealed class PlanOperation(
    IKeyedResolver<IOperationReporter> reporters,
    IMigrationHelper helper,
    IKeyedResolver<ISqlGenerator> sqlGenerator
) : IPlanOperation
{
    public async Task Execute(PlanArguments arguments, CancellationToken cancellationToken = default)
    {
        reporters.Current.Info("Planning schema migration. No changes will be applied to the database.");
        var plan = await helper.Plan(SchemaSourceMode.Offline, required: false, arguments.Schemas, cancellationToken);
        if (!sqlGenerator.HasCurrent)
        {
            reporters.Current.Info("Unable to generate SQL preview. No provider is configured.");
            return;
        }

        var sqlPlan = sqlGenerator.Current.Generate(plan);
        reporters.Current.ReportSqlPlan(sqlPlan);
    }
}
