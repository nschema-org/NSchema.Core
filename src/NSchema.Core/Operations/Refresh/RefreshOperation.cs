using NSchema.Diagnostics;
using NSchema.Operations.Services;
using NSchema.State;

namespace NSchema.Operations.Refresh;

internal sealed class RefreshOperation(IMigrationWorkflow workflow, IOperationReporter reporter, IStateLock? stateLock = null) : IRefreshOperation
{
    public async Task<Result> Execute(RefreshArguments arguments, CancellationToken cancellationToken = default)
    {
        // Refresh writes the live schema into the store, so it takes the lock too.
        var stateLockHandle = await StateLockGuard.AcquireOrSkip(stateLock, reporter, "refresh", arguments.SkipLock, cancellationToken);
        try
        {
            await workflow.Refresh(RefreshMode.Required, cancellationToken);
            return Result.Success();
        }
        finally
        {
            // Release with an uncancellable token so a cancelled refresh still frees its own lock.
            if (stateLockHandle is not null)
            {
                await stateLockHandle.Release(CancellationToken.None);
            }
        }
    }
}
