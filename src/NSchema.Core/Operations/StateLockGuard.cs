using NSchema.Diagnostics;
using NSchema.State;
using NSchema.State.Model;

namespace NSchema.Operations;

/// <summary>
/// Shared lock handling for the state-mutating operations.
/// </summary>
internal static class StateLockGuard
{
    /// <summary>
    /// Acquires the state lock for <paramref name="operation"/>.
    /// </summary>
    public static async Task<Result<IStateLockHandle>> AcquireOrSkip(
        IStateLock? stateLock,
        string operation,
        bool skipLock,
        CancellationToken cancellationToken)
    {
        // No backend lock to take — this is an ordinary offline run, not a deliberate skip, so say nothing.
        if (stateLock is null)
        {
            return Result<IStateLockHandle>.Success(NoOpStateLockHandle.Instance);
        }

        if (skipLock)
        {
            // Peek so the warning is honest: name the lock we are running past rather than ignoring it silently.
            var held = await stateLock.Peek(cancellationToken);
            var warning = Diagnostic.Warning(operation, held is null
                ? "Running without the state lock; make sure no other operation runs against this state at the same time."
                : $"Running without the state lock; the state is currently locked by {held.Who} " +
                  $"(operation '{held.Operation}', since {held.CreatedUtc:u}) — proceeding anyway.");
            return Result<IStateLockHandle>.From(NoOpStateLockHandle.Instance, [warning]);
        }

        try
        {
            return Result<IStateLockHandle>.Success(await stateLock.Acquire(new StateLockRequest(operation), cancellationToken));
        }
        catch (StateLockedException ex)
        {
            // Contention is a recoverable, user-facing outcome, not a bug — surface it as a failure the caller renders.
            return Result<IStateLockHandle>.Failure(ex.ExistingLock is { } held
                ? Diagnostic.Error(operation,
                    $"The state is locked by {held.Who} (operation '{held.Operation}', since {held.CreatedUtc:u}). " +
                    "Wait for it to finish, or re-run with --no-lock to proceed anyway.")
                : Diagnostic.Error(operation, ex.Message));
        }
    }
}
