using NSchema.Current.Locks.Backends;
using NSchema.Current.Locks;

namespace NSchema.Current.Storage.Backends;

/// <summary>
/// An in-memory state backend for disposable databases.
/// </summary>
internal sealed class EphemeralStateStore : ISchemaStateStore, IStateLock
{
    private readonly Lock _gate = new();
    private ReadOnlyMemory<byte>? _payload;
    private StateLockInfo? _held;

    public Task<ReadOnlyMemory<byte>?> Read(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_payload);
        }
    }

    public Task Write(ReadOnlyMemory<byte> state, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            // Copy: the caller's buffer must not be able to mutate the stored payload after the write.
            _payload = state.ToArray();
        }
        return Task.CompletedTask;
    }

    public Task<IStateLockHandle> Acquire(StateLockRequest request, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_held is { } holder)
            {
                throw new StateLockedException(
                    $"The state is locked by {holder.Who} (operation '{holder.Operation}', since {holder.CreatedUtc:u}).",
                    holder);
            }

            var now = DateTimeOffset.UtcNow;
            _held = new StateLockInfo(
                Id: Guid.NewGuid().ToString("N"),
                Operation: request.Operation,
                Who: $"{Environment.UserName}@{Environment.MachineName}",
                CreatedUtc: now,
                ExpiresUtc: request.TimeToLive is { } ttl ? now + ttl : null
            );
            return Task.FromResult<IStateLockHandle>(new Handle(this, _held));
        }
    }

    public Task<StateLockInfo?> Peek(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_held);
        }
    }

    public ValueTask Release(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _held = null;
        }
        return ValueTask.CompletedTask;
    }

    private sealed class Handle(EphemeralStateStore owner, StateLockInfo info) : IStateLockHandle
    {
        public StateLockInfo Info => info;

        public ValueTask Release(CancellationToken cancellationToken = default)
        {
            lock (owner._gate)
            {
                // Only clear a hold this handle owns; a force-released and re-acquired lock is someone else's.
                if (ReferenceEquals(owner._held, info))
                {
                    owner._held = null;
                }
            }
            return ValueTask.CompletedTask;
        }
    }
}
