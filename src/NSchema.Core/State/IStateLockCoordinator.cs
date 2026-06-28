using NSchema.Diagnostics;
using NSchema.State.Model;

namespace NSchema.State;

/// <summary>
/// The consumer-facing surface for managing state locks around an operation.
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

    /// <summary>
    /// Reads the lock currently held against the state, or <see langword="null"/> when the
    /// state is free or no lock backend is configured operation.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<StateLockInfo?> Peek(CancellationToken cancellationToken = default);
}
