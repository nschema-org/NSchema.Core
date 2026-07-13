using NSchema.Current;
using NSchema.Diff.Domain;
using NSchema.Operations.Progress;

namespace NSchema.Operations;

/// <summary>
/// Compares the recorded state against the live database and reports how the live database has drifted from it.
/// </summary>
internal sealed class DriftOperation(ICurrentSchemaProvider currentProvider, ISchemaComparer comparer, IProgress<OperationProgress> progress)
    : IOperation<DriftArguments, Result<DriftResult>>
{
    public async Task<Result<DriftResult>> Execute(DriftArguments args, CancellationToken cancellationToken = default)
    {
        progress.Report(OperationProgress.Step("Reading recorded state..."));
        var recorded = await currentProvider.GetSchema(SchemaSourceMode.Offline, args.Scope, cancellationToken);
        if (recorded.Value is not { } recordedSchema)
        {
            return Result.Failure<DriftResult>(recorded.Diagnostics);
        }

        progress.Report(OperationProgress.Step("Reading live database..."));
        var live = await currentProvider.GetSchema(SchemaSourceMode.Online, args.Scope, cancellationToken);
        if (live.Value is not { } liveSchema)
        {
            return Result.Failure<DriftResult>(live.Diagnostics);
        }

        // Diff direction: recorded -> live, so the changes describe how the live database has drifted from what we
        // recorded (an added object appears as Add, an out-of-band drop as Remove).
        var diff = comparer.Compare(recordedSchema, liveSchema);
        return Result.From(new DriftResult(diff), recorded.Diagnostics.Concat(live.Diagnostics));
    }
}
