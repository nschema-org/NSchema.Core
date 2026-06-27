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
        IStateLock? stateLock,
        IOperationReporter reporter,
        string operation,
        bool skipLock,
        CancellationToken cancellationToken)
    {
        // No backend lock to take — this is an ordinary offline run, not a deliberate skip, so say nothing.
        if (stateLock is null)
        {
            return null;
        }

        if (!skipLock)
        {
            return await stateLock.Acquire(new StateLockRequest(operation), cancellationToken);
        }

        // Peek so the report is honest: name the lock we are running past rather than ignoring it silently.
        var held = await stateLock.Peek(cancellationToken);
        reporter.Warn(held is null
            ? "Running without the state lock; make sure no other operation runs against this state at the same time."
            : $"Running without the state lock; the state is currently locked by {held.Who} " +
              $"(operation '{held.Operation}', since {held.CreatedUtc:u}) — proceeding anyway.");
        return null;
    }
}
