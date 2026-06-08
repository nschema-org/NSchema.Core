namespace NSchema.Operations.Show;

/// <summary>
/// Reads the recorded (offline) state from the state store.
/// </summary>
internal interface IShowOperation
{
    /// <summary>
    /// Executes the show operation.
    /// </summary>
    /// <param name="arguments">The arguments controlling what is shown.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task Execute(ShowArguments arguments, CancellationToken cancellationToken = default);
}
