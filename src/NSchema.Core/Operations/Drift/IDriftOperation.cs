namespace NSchema.Operations.Drift;

/// <summary>
/// Compares the recorded (offline) state against the live (online) database
/// and reports how the live database has drifted from the recorded state.
/// </summary>
internal interface IDriftOperation
{
    /// <summary>
    /// Executes the drift operation.
    /// </summary>
    /// <param name="arguments">The arguments controlling the drift check.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task Execute(DriftArguments arguments, CancellationToken cancellationToken = default);
}
