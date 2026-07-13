namespace NSchema.Plan.PlanFile;

/// <summary>
/// Thrown when a saved plan file cannot be deserialized into a <see cref="PlanFileEnvelope"/>.
/// </summary>
internal sealed class PlanFileDeserializationException : Exception
{
    /// <summary>
    /// Creates a new <see cref="PlanFileDeserializationException"/>.
    /// </summary>
    /// <param name="message">A description of the failure.</param>
    /// <param name="innerException">The underlying failure that caused deserialization to fail, if any.</param>
    public PlanFileDeserializationException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
