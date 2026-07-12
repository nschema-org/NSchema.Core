namespace NSchema.Current.Locks.Backends;

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
    /// Reads the currently-held lock without acquiring or modifying it.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The held lock's info, or <see langword="null"/> when the state is not locked.</returns>
    Task<StateLockInfo?> Peek(CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <summary>
    /// Releases whatever lock is currently held.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    ValueTask Release(CancellationToken cancellationToken = default);
}
