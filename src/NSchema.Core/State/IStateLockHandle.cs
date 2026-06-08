namespace NSchema.State;

/// <summary>
/// Represents a held state lock. Disposing the handle releases the lock. Disposal is idempotent.
/// </summary>
public interface IStateLockHandle : IAsyncDisposable
{
    /// <summary>
    /// The identifier of the held lock, for diagnostics and (later) force-unlock by id.
    /// </summary>
    string LockId { get; }
}
