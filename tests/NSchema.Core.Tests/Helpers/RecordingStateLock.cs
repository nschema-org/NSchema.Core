using NSchema.State;
using NSchema.State.Model;

namespace NSchema.Tests.Helpers;

/// <summary>
/// An <see cref="IStateLock"/> test double that records each acquisition and how many handles were released.
/// Set <see cref="OnAcquire"/> to simulate a contended lock by throwing <see cref="StateLockedException"/>.
/// </summary>
internal sealed class RecordingStateLock : IStateLock
{
    public List<StateLockRequest> Acquisitions { get; } = [];
    public int Released { get; private set; }
    public int ForceReleases { get; private set; }
    public int Peeks { get; private set; }
    public Func<StateLockRequest, Task>? OnAcquire { get; set; }

    /// <summary>The value returned from <see cref="Peek"/> (defaults to nothing held).</summary>
    public StateLockInfo? PeekResult { get; set; }

    public Task<StateLockInfo?> Peek(CancellationToken cancellationToken = default)
    {
        Peeks++;
        return Task.FromResult(PeekResult);
    }

    public async Task<IStateLockHandle> Acquire(StateLockRequest request, CancellationToken cancellationToken = default)
    {
        if (OnAcquire is not null)
        {
            await OnAcquire(request);
        }

        Acquisitions.Add(request);

        // Deterministic clock so tests can assert on the recorded expiry when a time-to-live is requested.
        var createdUtc = DateTimeOffset.UnixEpoch;
        var info = new StateLockInfo(
            Id: "test",
            Operation: request.Operation,
            Who: "tester@host",
            CreatedUtc: createdUtc,
            ExpiresUtc: request.TimeToLive is { } ttl ? createdUtc + ttl : null);
        return new Handle(this, info);
    }

    public ValueTask Release(CancellationToken cancellationToken = default)
    {
        ForceReleases++;
        return ValueTask.CompletedTask;
    }

    private sealed class Handle(RecordingStateLock owner, StateLockInfo info) : IStateLockHandle
    {
        public StateLockInfo Info => info;

        public ValueTask Release(CancellationToken cancellationToken = default)
        {
            owner.Released++;
            return ValueTask.CompletedTask;
        }
    }
}
