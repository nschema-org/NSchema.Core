using NSchema.State.Model;

namespace NSchema.State;

/// <summary>
/// Represents a held state lock.
/// </summary>
public interface IStateLockHandle
{
    /// <summary>
    /// Metadata about the held lock.
    /// </summary>
    StateLockInfo Info { get; }

    /// <summary>
    /// Releases the held lock. Idempotent: releasing more than once is a no-op.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    ValueTask Release(CancellationToken cancellationToken = default);
}
