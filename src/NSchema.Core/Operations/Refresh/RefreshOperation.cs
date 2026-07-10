using NSchema.Diagnostics;
using NSchema.Operations.Services;

namespace NSchema.Operations.Refresh;

/// <summary>
/// Reads the live current schema and writes it to the state store, under the state lock.
/// </summary>
internal sealed class RefreshOperation(IMigrationWorkflow workflow) : IOperation<RefreshArguments, Result<RefreshResult>>
{
    public async Task<Result<RefreshResult>> Execute(RefreshArguments args, CancellationToken cancellationToken = default)
    {
        var captured = await workflow.Refresh(null, args.Force, cancellationToken);
        if (captured is null)
        {
            return Result.Failure<RefreshResult>(Diagnostic.Error("refresh", "Unable to refresh state without a configured state store."));
        }

        if (captured.IsFailure)
        {
            return Result.Failure<RefreshResult>(captured.Diagnostics);
        }

        var capture = captured.Value;
        return Result.Success(new RefreshResult(capture.Schema, capture.SnapshotBytes), captured.Diagnostics);
    }
}
