using NSchema.Deployment;
using NSchema.Diff.Model.Services;
using NSchema.Operations.Progress;
using NSchema.Project.Model.Directives;
using NSchema.State;
using NSchema.State.Model;

namespace NSchema.Operations;

/// <summary>
/// Compares the recorded state against the live database and reports how the live database has drifted from it.
/// </summary>
internal sealed class DriftOperation(IDatabaseProvider provider, IDatabaseStateManager stateManager, IDatabaseComparer comparer, IProgress<OperationProgress> progress)
    : IOperation<DriftArguments, Result<DriftResult>>
{
    public async Task<Result<DriftResult>> Execute(DriftArguments args, CancellationToken cancellationToken = default)
    {
        progress.Report(OperationProgress.Step("Reading recorded state..."));
        var recorded = await stateManager.Read(new StateReadArguments(), cancellationToken);
        if (recorded.Value is not { } recordedState)
        {
            return Result.Failure<DriftResult>(recorded.Diagnostics);
        }
        // Before anything is recorded, drift measures against nothing.
        var recordedSchema = (recordedState.State ?? DatabaseState.Empty).Database.ScopedTo(args.Scope);

        progress.Report(OperationProgress.Step("Reading live database..."));
        var live = await provider.GetDatabase(args.Scope, cancellationToken);
        if (live.Value is not { } liveSchema)
        {
            return Result.Failure<DriftResult>(live.Diagnostics);
        }

        // Diff direction: recorded -> live, so the changes describe how the live database has drifted from what we
        // recorded (an added object appears as Add, an out-of-band drop as Remove).
        var diff = comparer.Compare(recordedSchema, liveSchema, ProjectDirectives.Empty);
        return Result.From(new DriftResult(diff), recorded.Diagnostics.Concat(live.Diagnostics));
    }
}
