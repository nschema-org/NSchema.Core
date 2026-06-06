using NSchema.Hosting.Services;
using NSchema.Migration;
using NSchema.Resolution;
using NSchema.Schema;
using NSchema.Sql;

namespace NSchema.Hosting.Operations;

internal sealed class PlanOperation(
    IKeyedResolver<IMigrationReporter> reporters,
    IMigrationHelper helper,
    IKeyedResolver<ISqlGenerator> sqlGenerator
) : IMigrationOperation
{
    public async Task Execute(CancellationToken cancellationToken = default)
    {
        reporters.Current.Info("Planning schema migration. No changes will be applied to the database.");
        var plan = await helper.Plan(SchemaSourceMode.Offline, required: false, cancellationToken);
        if (!sqlGenerator.HasCurrent)
        {
            reporters.Current.Info("Unable to generate SQL preview. No provider is configured.");
            return;
        }

        var sqlPlan = sqlGenerator.Current.Generate(plan);
        reporters.Current.ReportSqlPlan(sqlPlan);
    }
}
