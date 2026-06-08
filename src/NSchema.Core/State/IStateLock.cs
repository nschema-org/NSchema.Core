namespace NSchema.State;

/// <summary>
/// Coordinates exclusive access to the shared state.
/// </summary>
public interface IStateLock
{
    /// <summary>
    /// Acquires the lock, blocking other operations until the returned handle is disposed.
    /// </summary>
    /// <param name="request">Describes the operation acquiring the lock.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A handle that releases the lock when disposed.</returns>
    /// <exception cref="StateLockedException">The lock is already held by another operation.</exception>
    Task<IStateLockHandle> Acquire(StateLockRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forcibly removes the current lock regardless of who holds it, for recovering from a stale lock.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The info of the lock that was removed, or <see langword="null"/> if nothing was locked.</returns>
    Task<StateLockInfo?> ForceUnlock(CancellationToken cancellationToken = default);
}
