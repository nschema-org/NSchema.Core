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
    public async Task Execute(PlanArguments arguments, CancellationToken cancellationToken = default)
    {
        reporter.Announce("Planning schema migration. No changes will be applied to the database.");
        var planned = await workflow.Plan(SchemaSourceMode.Offline, required: false, arguments.Schemas, cancellationToken);
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
            reporter.Success($"Plan saved to {arguments.OutFile}. Apply it later with this file to execute exactly this plan.");
        }
    }
}
