using NSchema.Operations.Services;
using NSchema.State;

namespace NSchema.Operations.Refresh;

internal sealed class RefreshOperation(IMigrationWorkflow workflow, IStateLock stateLock) : IRefreshOperation
{
    public async Task Execute(RefreshArguments arguments, CancellationToken cancellationToken = default)
    {
        // Refresh writes the live schema into the store, so it takes the lock too (no-op unless one is registered).
        await using var stateLockHandle = await stateLock.Acquire(new StateLockRequest("refresh"), cancellationToken);
        await workflow.Refresh(RefreshMode.Required, cancellationToken);
    }
}
