namespace NSchema.Current.Locks;

/// <summary>
/// The consumer-facing surface for managing state locks.
/// </summary>
public interface IStateLockManager
{
    /// <summary>
    /// Takes the state lock described by <paramref name="arguments"/>.
    /// </summary>
    /// <param name="arguments">The lock to take.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<Result<IStateLockHandle>> Acquire(AcquireLockArguments arguments, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the lock currently held against the state — the holder's details, or <see langword="null"/> when the
    /// state is free or no lock backend is configured — without acquiring it, so it never contends with a running
    /// operation.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<StateLockInfo?> Peek(CancellationToken cancellationToken = default);

    /// <summary>
    /// Force-releases whatever lock is currently held — regardless of who holds it — and returns the released lock's
    /// details, or <see langword="null"/> when the state was already free or no lock backend is configured.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<StateLockInfo?> Release(CancellationToken cancellationToken = default);
}
