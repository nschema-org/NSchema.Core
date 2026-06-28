using NSchema.Diagnostics;

namespace NSchema.State;

/// <summary>
/// The consumer-facing surface for taking the state lock around an operation.
/// </summary>
public interface IStateLockCoordinator
{
    /// <summary>
    /// Takes the state lock for <paramref name="operation"/>:
    /// <list type="bullet">
    /// <item>no lock backend, or a <paramref name="skipLock"/> run, yields a no-op handle — the skip also carries a
    /// warning naming the lock it ran past;</item>
    /// <item>a successful acquire yields the real handle;</item>
    /// <item>a lock already held by another operation is a failure carrying the holder's details.</item>
    /// </list>
    /// The handle is never <see langword="null"/> on success and is disposed to release the lock.
    /// </summary>
    /// <param name="operation">The operation taking the lock (recorded against the lock).</param>
    /// <param name="skipLock">When <see langword="true"/>, runs without acquiring (e.g. <c>--no-lock</c>).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<Result<IStateLockHandle>> Acquire(string operation, bool skipLock = false, CancellationToken cancellationToken = default);
}
