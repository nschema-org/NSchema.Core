using NSchema.Diagnostics;
using NSchema.Operations.Services;
using NSchema.Plan.PlanFile;
using NSchema.Schema;
using NSchema.Sql;
using NSchema.Sql.Model;

namespace NSchema.Operations.Plan;

internal sealed class PlanOperation(
    IMigrationWorkflow workflow,
    IPlanFileWriter handler,
    ISqlGenerator? sqlGenerator = null
) : IPlanOperation
{
    public async Task<Result<PlanResult>> Execute(PlanArguments arguments, CancellationToken cancellationToken = default)
    {
        var planned = await workflow.ComputePlan(SchemaSourceMode.Offline, required: false, arguments.Schemas, cancellationToken);
        var diagnostics = new List<Diagnostic>(planned.Diagnostics);
        SqlPlan? sql = null;

        if (!planned.HasErrors)
        {
            if (sqlGenerator is not null)
            {
                var generated = sqlGenerator.Generate(planned.Plan);
                sql = generated;
                if (arguments.OutFile is not null)
                {
                    var envelope = new PlanFileEnvelope(planned.Plan, generated, planned.Diff, DateTimeOffset.UtcNow);
                    await handler.Write(arguments.OutFile, envelope, cancellationToken);
                }
            }
            else if (arguments.OutFile is not null)
            {
                // Without a provider there is no SQL to write.
                diagnostics.Add(Diagnostic.Error("plan", "Saving a plan to a file requires a database provider to generate SQL, but none is registered."));
            }
            else
            {
                diagnostics.Add(Diagnostic.Warning("plan", "Unable to generate SQL preview. No provider is configured."));
            }
        }

        return Result<PlanResult>.From(new PlanResult(planned.Diff, sql), diagnostics);
    }
}
