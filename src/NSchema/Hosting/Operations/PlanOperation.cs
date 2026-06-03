using NSchema.Hosting.Services;
using NSchema.Migration;
using NSchema.Schema;
using NSchema.Sql;

namespace NSchema.Hosting.Operations;

internal sealed class PlanOperation(
    IMigrationReporter reporter,
    IMigrationHelper helper,
    ISqlGenerator? sqlGenerator = null
) : IMigrationOperation
{
    public async Task Execute(CancellationToken cancellationToken = default)
    {
        reporter.Info("Planning schema migration. No changes will be applied to the database.");
        var plan = await helper.Plan(SchemaSourceMode.Offline, required: false, cancellationToken);

        // Generating SQL is pure string building, so the preview works offline whenever a dialect (ISqlPlanner)
        // is registered — no database connection required.
        if (sqlGenerator is null)
        {
            reporter.Info("Unable to generate SQL preview. No provider is configured.");
            return;
        }

        reporter.Info("Generating SQL...");
        reporter.ReportSqlPlan(sqlGenerator.Generate(plan));
    }
}
