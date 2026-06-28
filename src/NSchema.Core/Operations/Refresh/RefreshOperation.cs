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
        var capture = await workflow.Refresh(cancellationToken);
        return capture is null
            ? Result.Failure<RefreshResult>(Diagnostic.Error("refresh", "Unable to refresh state without a configured state store."))
            : Result.Success(new RefreshResult(capture.Schema, capture.SnapshotBytes));
    }
}
