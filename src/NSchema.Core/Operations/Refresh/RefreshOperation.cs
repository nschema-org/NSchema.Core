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
        var (handle, warning) = await StateLockGuard.AcquireOrSkip(stateLock, "refresh", args.SkipLock, cancellationToken);
        try
        {
            await workflow.Refresh(RefreshMode.Required, cancellationToken);
            var diagnostics = new List<Diagnostic>();
            if (warning is not null)
            {
                diagnostics.Add(warning);
            }
            return Result.Success(diagnostics);
        }
        finally
        {
            // Release with an uncancellable token so a cancelled refresh still frees its own lock.
            if (handle is not null)
            {
                await handle.Release(CancellationToken.None);
            }
        }
    }
}
