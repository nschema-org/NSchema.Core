using NSchema.State.Model;

namespace NSchema.State;

/// <summary>
/// The default <see cref="IStateLock"/>: acquiring is a no-op and the returned handle releases nothing. This keeps
/// operations able to acquire a lock unconditionally; locking is off until a real implementation is registered
/// (e.g. via <c>UseFileStateLock(...)</c> or <c>UseStateLock&lt;T&gt;()</c>).
/// </summary>
internal sealed class NoOpStateLock : IStateLock
{
    public Task<IStateLockHandle> Acquire(StateLockRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult<IStateLockHandle>(Handle.Instance);

    public Task<StateLockInfo?> Peek(CancellationToken cancellationToken = default) =>
        Task.FromResult<StateLockInfo?>(null);

    public Task<StateLockInfo?> ForceUnlock(CancellationToken cancellationToken = default) =>
        Task.FromResult<StateLockInfo?>(null);

    private sealed class Handle : IStateLockHandle
    {
        public static readonly Handle Instance = new();

        // Locking is off, so there is no real lock to describe; a synthetic info keeps the contract satisfied.
        public StateLockInfo Info { get; } = new("noop", "noop", "noop", default);

        public ValueTask Release(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
