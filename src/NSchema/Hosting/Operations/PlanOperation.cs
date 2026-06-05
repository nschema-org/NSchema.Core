using NSchema.Hosting.Services;
using NSchema.Schema;
using NSchema.Sql;

namespace NSchema.Hosting.Operations;

internal sealed class PlanOperation(
    IMigrationReporterResolver reporter,
    IMigrationHelper helper,
    ISqlGenerator? sqlGenerator = null
) : IMigrationOperation
{
    public async Task Execute(CancellationToken cancellationToken = default)
    {
        reporter.Current.Info("Planning schema migration. No changes will be applied to the database.");
        var plan = await helper.Plan(SchemaSourceMode.Offline, required: false, cancellationToken);
        if (sqlGenerator is null)
        {
            reporter.Current.Info("Unable to generate SQL preview. No provider is configured.");
            return;
        }
        reporter.Current.Info("Generating SQL...");
        reporter.Current.ReportSqlPlan(sqlGenerator.Generate(plan));
    }
}
