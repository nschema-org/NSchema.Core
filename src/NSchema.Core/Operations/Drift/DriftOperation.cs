using NSchema.Diagnostics;
using NSchema.Diff;
using NSchema.Schema;

using NSchema.Operations.Progress;

namespace NSchema.Operations.Drift;

internal sealed class DriftOperation(
    ICurrentSchemaProvider currentProvider,
    IOperationReporter reporter,
    IProgress<OperationProgress> progress,
    ISchemaComparer comparer
) : IDriftOperation
{
    public async Task<Result<DriftResult>> Execute(DriftArguments arguments, CancellationToken cancellationToken = default)
    {
        reporter.Announce("Checking for drift between the recorded state and the live database...");
        progress.Report(OperationProgress.Step("Reading recorded state..."));
        var recorded = await currentProvider.GetSchema(SchemaSourceMode.Offline, arguments.Schemas, required: true, cancellationToken);

        progress.Report(OperationProgress.Step("Reading live database..."));
        var live = await currentProvider.GetSchema(SchemaSourceMode.Online, arguments.Schemas, required: true, cancellationToken);

        // Diff direction: recorded -> live, so the changes describe how the live database has drifted from
        // what we recorded (an added object appears as Add, an out-of-band drop as Remove). This is a pure
        // observation: no transformers or policies run, so it never fails on policy violations.
        var diff = comparer.Compare(recorded, live);

        reporter.ReportDiff(diff);

        if (diff.IsEmpty)
        {
            reporter.Success("No drift detected.");
        }
        else
        {
            reporter.Warn($"Drift detected: {RunSummary.Describe(diff)}.");
        }

        // Drift is observational — it never fails on policy — so this is always a success carrying the diff.
        return Result<DriftResult>.Success(new DriftResult(diff));
    }
}
