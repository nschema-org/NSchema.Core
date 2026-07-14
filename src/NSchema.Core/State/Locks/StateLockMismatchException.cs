namespace NSchema.State.Locks;

/// <summary>
/// Thrown when a force-unlock is requested with a specific lock id that does not match the lock currently held.
/// </summary>
public sealed class StateLockMismatchException : Exception
{
    /// <summary>
    /// The lock id the caller asked to release.
    /// </summary>
    public string RequestedLockId { get; }

    /// <summary>
    /// The lock actually held when the force-unlock was attempted.
    /// </summary>
    public StateLockInfo HeldLock { get; }

    /// <summary>
    /// Creates a new <see cref="StateLockMismatchException"/>.
    /// </summary>
    /// <param name="requestedLockId">The lock id the caller asked to release.</param>
    /// <param name="heldLock">The lock actually held.</param>
    public StateLockMismatchException(string requestedLockId, StateLockInfo heldLock)
        : base($"The held lock '{heldLock.Id}' does not match the requested lock id '{requestedLockId}'. " +
               "The lock may have changed since you read it; re-check the current lock before forcing it.")
    {
        RequestedLockId = requestedLockId;
        HeldLock = heldLock;
    }
}
