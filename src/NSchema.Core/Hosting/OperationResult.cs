namespace NSchema.Hosting;

/// <summary>
/// Carries the outcome of a migration run from the hosted <see cref="NSchemaHost"/> back to the caller.
/// </summary>
internal sealed class OperationResult
{
    /// <summary>
    /// The exception that aborted the run, or <see langword="null"/> if it completed successfully.
    /// </summary>
    public Exception? Exception { get; set; }
}
