using NSchema.State.Model;

namespace NSchema.State;

/// <summary>
/// A null-object <see cref="IStateLockHandle"/> for the cases where no real lock is held.
/// </summary>
internal sealed class NoOpStateLockHandle : IStateLockHandle
{
    /// <summary>The shared instance — the handle is stateless.</summary>
    public static readonly NoOpStateLockHandle Instance = new();

    private NoOpStateLockHandle()
    {
    }

    /// <summary>
    /// No lock is held, so there is no lock metadata to report.
    /// </summary>
    public StateLockInfo Info => throw new NotSupportedException("No lock is held; this handle represents an unlocked run.");

    public ValueTask Release(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
