using NSchema.Plan.PlanFile;
using NSchema.Schema;

namespace NSchema.Operations.Show;

internal sealed class ShowOperation(
    ICurrentSchemaProvider currentProvider,
    IOperationReporter reporter,
    IPlanFileWriter planFile
) : IShowOperation
{
    public async Task Execute(ShowArguments arguments, CancellationToken cancellationToken = default)
    {
        if (arguments.PlanFile is { } path)
        {
            await ShowPlanFile(path, cancellationToken);
            return;
        }

        reporter.Announce("Showing recorded state. The live database will not be contacted.");

        reporter.Progress("Reading recorded state...");
        var recorded = await currentProvider.GetSchema(SchemaSourceMode.Offline, arguments.Schemas, required: true, cancellationToken);
        reporter.ReportSchema(recorded);
    }

    /// <summary>
    /// Reports a saved plan file's diff, plan, and SQL — the same view the plan step produced — without
    /// contacting the live database or the state store. The analogue of <c>terraform show &lt;planfile&gt;</c>.
    /// </summary>
    private async Task ShowPlanFile(string path, CancellationToken cancellationToken)
    {
        reporter.Announce($"Showing saved plan from {path}. No database or state store will be contacted.");

        var envelope = await planFile.Read(path, cancellationToken);
        reporter.ReportDiff(envelope.Diff);
        reporter.ReportPlan(envelope.Plan);
        reporter.ReportSqlPlan(envelope.Sql);
    }
}
