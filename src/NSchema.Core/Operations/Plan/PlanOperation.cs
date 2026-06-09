using NSchema.Operations.Services;
using NSchema.Plan.PlanFile;
using NSchema.Resolution;
using NSchema.Schema;
using NSchema.Sql;

namespace NSchema.Operations.Plan;

internal sealed class PlanOperation(
    IKeyedResolver<IOperationReporter> reporters,
    IMigrationWorkflow workflow,
    IKeyedResolver<ISqlGenerator> sqlGenerator,
    IPlanFileWriter handler
) : IPlanOperation
{
    public async Task Execute(PlanArguments arguments, CancellationToken cancellationToken = default)
    {
        reporters.Current.Info("Planning schema migration. No changes will be applied to the database.");
        var planned = await workflow.Plan(SchemaSourceMode.Offline, required: false, arguments.Schemas, cancellationToken);
        if (!sqlGenerator.HasCurrent)
        {
            if (arguments.OutFile is not null)
            {
                throw new InvalidOperationException("Saving a plan to a file requires a database provider to generate SQL, but none is registered.");
            }

            reporters.Current.Info("Unable to generate SQL preview. No provider is configured.");
            return;
        }

        var sqlPlan = sqlGenerator.Current.Generate(planned.Plan);
        reporters.Current.ReportSqlPlan(sqlPlan);

        if (arguments.OutFile is not null)
        {
            var envelope = new PlanFileEnvelope(planned.Plan, sqlPlan, planned.Diff, DateTimeOffset.UtcNow);
            await handler.Write(arguments.OutFile, envelope, cancellationToken);
            reporters.Current.Info($"Plan saved to {arguments.OutFile}. Apply it later with this file to execute exactly this plan.");
        }
    }
}
