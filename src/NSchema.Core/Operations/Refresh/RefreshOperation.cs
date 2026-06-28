using NSchema.Diagnostics;
using NSchema.Operations.Services;
using NSchema.State;

namespace NSchema.Operations.Refresh;

/// <summary>
/// Reads the live current schema and writes it to the state store, under the state lock.
/// </summary>
internal sealed class RefreshOperation(IMigrationWorkflow workflow, IStateLock? stateLock = null)
    : IOperation<RefreshArguments, Result>
{
    public async Task<Result> Execute(RefreshArguments args, CancellationToken cancellationToken = default)
    {
        // Refresh writes the live schema into the store, so it takes the lock too.
        var lockResult = await StateLockGuard.AcquireOrSkip(stateLock, "refresh", args.SkipLock, cancellationToken);
        if (lockResult.IsFailure)
        {
            return Result.Failure(lockResult.Errors);
        }

        try
        {
            await workflow.Refresh(RefreshMode.Required, cancellationToken);
            // Include any non-fatal diagnostics that might be reported.
            return Result.Success(lockResult.Diagnostics);
        }
        finally
        {
            // Release with an uncancellable token so a cancelled refresh still frees its own lock.
            await lockResult.Value.Release(CancellationToken.None);
        }
    }
}
