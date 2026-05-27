namespace NSchema.Hosting;

/// <summary>
/// Runs the end-to-end migration: planning, user-facing reporting, and execution.
/// </summary>
internal interface IMigrationPipeline
{
    /// <summary>
    /// Executes the pipeline.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task Run(CancellationToken cancellationToken = default);
}
