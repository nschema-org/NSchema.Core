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
    public static async Task<IStateLockHandle?> AcquireOrSkip(
        IStateLock stateLock,
        IOperationReporter reporter,
        string operation,
        bool skipLock,
        CancellationToken cancellationToken)
    {
        if (!skipLock)
        {
            return await stateLock.Acquire(new StateLockRequest(operation), cancellationToken);
        }

        // Peek so the skip is honest: report a lock we are deliberately running past, rather than silently ignoring it.
        var held = await stateLock.Peek(cancellationToken);
        reporter.Warn(held is null
            ? "Skipping the state lock (--no-lock); make sure no other operation runs against this state at the same time."
            : $"Skipping the state lock (--no-lock); the state is currently locked by {held.Who} " +
              $"(operation '{held.Operation}', since {held.CreatedUtc:u}) — proceeding anyway.");
        return null;
    }
}
