using NSchema.Diagnostics;
using NSchema.State.Model;

namespace NSchema.State;

/// <summary>
/// Convenience proxies over <see cref="IStateLockCoordinator"/>.
/// </summary>
public static class StateLockCoordinatorExtensions
{
    /// <param name="coordinator">The lock coordinator.</param>
    extension(IStateLockCoordinator coordinator)
    {
        /// <summary>
        /// Takes the state lock for <paramref name="operation"/> with no expiry.
        /// </summary>
        /// <param name="operation">The operation taking the lock (recorded against the lock).</param>
        /// <param name="skipLock">When <see langword="true"/>, runs without acquiring (e.g. <c>--no-lock</c>).</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        public Task<Result<IStateLockHandle>> Acquire(string operation, bool skipLock = false, CancellationToken cancellationToken = default) =>
            coordinator.Acquire(new StateLockRequest(operation), skipLock, cancellationToken);
    }
}
