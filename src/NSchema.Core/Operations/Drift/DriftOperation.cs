using NSchema.Diagnostics;
using NSchema.Diff;
using NSchema.Schema;

using NSchema.Operations.Progress;

namespace NSchema.Operations.Drift;

internal sealed class DriftOperation(
    ICurrentSchemaProvider currentProvider,
    IProgress<OperationProgress> progress,
    ISchemaComparer comparer
) : IDriftOperation
{
    public async Task<Result<DriftResult>> Execute(DriftArguments arguments, CancellationToken cancellationToken = default)
    {
        progress.Report(OperationProgress.Step("Reading recorded state..."));
        var recorded = await currentProvider.GetSchema(SchemaSourceMode.Offline, arguments.Schemas, required: true, cancellationToken);

        progress.Report(OperationProgress.Step("Reading live database..."));
        var live = await currentProvider.GetSchema(SchemaSourceMode.Online, arguments.Schemas, required: true, cancellationToken);

        // Diff direction: recorded -> live, so the changes describe how the live database has drifted from
        // what we recorded (an added object appears as Add, an out-of-band drop as Remove).
        var diff = comparer.Compare(recorded, live);
        return Result<DriftResult>.Success(new DriftResult(diff));
    }
}
