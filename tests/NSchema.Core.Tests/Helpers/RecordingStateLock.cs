using NSchema.State;
using NSchema.State.Model;

namespace NSchema.Tests.Helpers;

/// <summary>
/// An <see cref="IStateLock"/> test double that records each acquisition and how many handles were released
/// (disposed). Set <see cref="OnAcquire"/> to simulate a contended lock by throwing <see cref="StateLockedException"/>.
/// </summary>
internal sealed class RecordingStateLock : IStateLock
{
    public List<StateLockRequest> Acquisitions { get; } = [];
    public int Released { get; private set; }
    public int ForceUnlocks { get; private set; }
    public Func<StateLockRequest, Task>? OnAcquire { get; set; }

    /// <summary>The value returned from <see cref="ForceUnlock"/> (defaults to nothing held).</summary>
    public StateLockInfo? ForceUnlockResult { get; set; }

    public async Task<IStateLockHandle> Acquire(StateLockRequest request, CancellationToken cancellationToken = default)
    {
        if (OnAcquire is not null)
        {
            await OnAcquire(request);
        }

        Acquisitions.Add(request);
        return new Handle(this);
    }

    public Task<StateLockInfo?> ForceUnlock(CancellationToken cancellationToken = default)
    {
        ForceUnlocks++;
        return Task.FromResult(ForceUnlockResult);
    }

    private sealed class Handle(RecordingStateLock owner) : IStateLockHandle
    {
        public string LockId => "test";

        public ValueTask DisposeAsync()
        {
            owner.Released++;
            return ValueTask.CompletedTask;
        }
    }
}
