namespace NSchema.Current.Locks;

/// <summary>
/// Thrown when a state lock cannot be acquired because it is already held by another operation.
/// </summary>
public sealed class StateLockedException : Exception
{
    /// <summary>
    /// Information about the lock currently held, when the implementation could read it; otherwise <see langword="null"/>.
    /// </summary>
    public StateLockInfo? ExistingLock { get; }

    /// <summary>
    /// Creates a new <see cref="StateLockedException"/>.
    /// </summary>
    /// <param name="message">A description of the failure.</param>
    /// <param name="existingLock">Information about the held lock, if available.</param>
    public StateLockedException(string message, StateLockInfo? existingLock = null) : base(message)
    {
        ExistingLock = existingLock;
    }
}
