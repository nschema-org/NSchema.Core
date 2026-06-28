using NSchema.Diagnostics;
using NSchema.Operations.Services;
using NSchema.Plan.Model;
using NSchema.Plan.PlanFile;
using NSchema.Schema;
using NSchema.Sql;
using NSchema.Sql.Model;

namespace NSchema.Operations.Plan;

/// <summary>
/// Computes a migration plan.
/// </summary>
internal sealed class PlanOperation(IMigrationWorkflow workflow, IPlanFileWriter planFile, ISqlGenerator? sqlGenerator = null)
    : IOperation<PlanArguments, Result<PlanResult>>
{
    public async Task<Result<PlanResult>> Execute(PlanArguments args, CancellationToken cancellationToken = default)
    {
        // A teardown reads the managed schema (recorded, or the desired files); a preview reads the recorded state with
        // a live fallback; an apply must read the live database.
        var planned = args.Target switch
        {
            PlanTarget.Teardown => await workflow.ComputeTeardown(cancellationToken),
            PlanTarget.Live => await workflow.ComputePlan(SchemaSourceMode.Online, required: true, args.Schemas, cancellationToken),
            _ => await workflow.ComputePlan(SchemaSourceMode.Offline, required: false, args.Schemas, cancellationToken),
        };

        var diagnostics = new List<Diagnostic>(planned.Diagnostics);

        // The diff rides along even on a policy failure (so the offending change stays visible); the plan and SQL are
        // only produced for a successful planning pass.
        var diff = planned.Value?.Diff;
        MigrationPlan? plan = null;
        SqlPlan? sql = null;

        if (planned.IsSuccess)
        {
            plan = planned.Value.Plan;
            if (sqlGenerator is not null)
            {
                sql = sqlGenerator.Generate(planned.Value.Plan);
                if (args.OutFile is not null)
                {
                    var envelope = new PlanFileEnvelope(planned.Value.Diff, planned.Value.Plan, sql, DateTimeOffset.UtcNow);
                    await planFile.Write(args.OutFile, envelope, cancellationToken);
                }
            }
            else if (args.OutFile is not null)
            {
                // Without a provider there is no SQL to write.
                diagnostics.Add(Diagnostic.Error("plan", "Saving a plan to a file requires a database provider to generate SQL, but none is registered."));
            }
            else
            {
                diagnostics.Add(Diagnostic.Warning("plan", "Unable to generate SQL preview. No provider is configured."));
            }
        }

        return Result.From(new PlanResult(diff, plan, sql), diagnostics);
    }
}
