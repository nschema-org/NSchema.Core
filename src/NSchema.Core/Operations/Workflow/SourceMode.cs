namespace NSchema.Operations.Workflow;

/// <summary>
/// Which current source a plan's current side reads from.
/// </summary>
internal enum SourceMode
{
    /// <summary>
    /// Read the live deployment. Required for an apply; captures real state after migration.
    /// </summary>
    Live,

    /// <summary>
    /// Read the recorded state snapshot. Used for offline planning without a database connection.
    /// </summary>
    Recorded,
}
