namespace NSchema.Plan.PlanFile;

internal interface IPlanFileWriter
{
    /// <summary>
    /// Reads the envelope from the given file.
    /// </summary>
    /// <exception cref="InvalidOperationException">No SQL dialect is configured (a saved plan is dialect-specific).</exception>
    Task<PlanFileEnvelope> Read(string path, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the plan to the given file.
    /// </summary>
    /// <exception cref="InvalidOperationException">No SQL dialect is configured (a saved plan is dialect-specific).</exception>
    Task Write(string path, PlanFileEnvelope envelope, CancellationToken cancellationToken);
}
