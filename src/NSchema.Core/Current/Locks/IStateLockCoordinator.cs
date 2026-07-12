namespace NSchema.Current.Locks;

/// <summary>
/// The consumer-facing surface for managing state locks — taking, inspecting, and releasing them. It is the surface a
/// front-end takes a scoped operation lock with, holds a long-lived manual lock through (acquire and never release,
/// since the handle outlives the process), or force-releases a lock from another process.
/// </summary>
public interface IStateLockCoordinator
{
    /// <summary>
    /// Takes the state lock described by <paramref name="request"/> (its operation name, and optional time-to-live):
    /// <list type="bullet">
    /// <item>no lock backend, or a <paramref name="skipLock"/> run, yields a no-op handle — the skip also carries a
    /// warning naming the lock it ran past;</item>
    /// <item>a successful acquire yields the real handle (non-<see langword="null"/>) — release it explicitly, or never
    /// release it to hold the lock past this process;</item>
    /// <item>a lock already held by another operation is a failure carrying the holder's details.</item>
    /// </list>
    /// </summary>
    /// <param name="request">The lock to take (operation name and optional expiry).</param>
    /// <param name="skipLock">When <see langword="true"/>, runs without acquiring (e.g. <c>--no-lock</c>).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<Result<IStateLockHandle>> Acquire(StateLockRequest request, bool skipLock = false, CancellationToken cancellationToken = default);

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
