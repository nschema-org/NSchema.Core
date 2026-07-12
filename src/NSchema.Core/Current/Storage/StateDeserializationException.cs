namespace NSchema.Current.Storage;

/// <summary>
/// Thrown when a stored state payload cannot be deserialized into a schema snapshot.
/// </summary>
public sealed class StateDeserializationException : Exception
{
    /// <summary>
    /// Creates a new <see cref="StateDeserializationException"/>.
    /// </summary>
    /// <param name="message">A description of the failure.</param>
    /// <param name="innerException">The underlying failure that caused deserialization to fail, if any.</param>
    public StateDeserializationException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
