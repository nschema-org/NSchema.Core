using NSchema.Current;
using NSchema.Operations.Workflow;
using NSchema.Plan.PlanFile;

namespace NSchema.Operations;

/// <summary>
/// Computes a migration plan.
/// </summary>
internal sealed class PlanOperation(IMigrationWorkflow workflow, IPlanFileManager planFile)
    : IOperation<PlanArguments, Result<PlanResult>>
{
    public async Task<Result<PlanResult>> Execute(PlanArguments args, CancellationToken cancellationToken = default)
    {
        // A teardown reads the managed (recorded) schema; a preview reads the recorded state; an apply must read
        // the live database.
        var planned = args.Target switch
        {
            PlanTarget.Teardown => await workflow.ComputeTeardown(cancellationToken),
            PlanTarget.Live => await workflow.ComputePlan(SchemaSourceMode.Online, args.Schemas, cancellationToken),
            _ => await workflow.ComputePlan(SchemaSourceMode.Offline, args.Schemas, cancellationToken),
        };

        if (planned.Value is { } plan && args.OutFile is not null)
        {
            await planFile.Write(args.OutFile, new PlanFileEnvelope(plan, DateTimeOffset.UtcNow), cancellationToken);
        }

        // The plan rides along even on a policy failure, so the offending change stays visible.
        return Result.From(new PlanResult(planned.Value), planned.Diagnostics);
    }
}
