namespace NSchema.Plan.PlanFile;

internal interface IPlanFileWriter
{
    /// <summary>
    /// Reads the envelope from the given file.
    /// </summary>
    /// <exception cref="PlanFileDeserializationException">The file is corrupt, truncated, or otherwise could not be deserialized.</exception>
    Task<PlanFileEnvelope> Read(string path, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the plan to the given file.
    /// </summary>
    Task Write(string path, PlanFileEnvelope envelope, CancellationToken cancellationToken);
}
