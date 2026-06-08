namespace NSchema.Operations.Destroy;

/// <summary>
/// Tears down the managed schema, dropping all managed objects from the target.
/// </summary>
internal interface IDestroyOperation
{
    /// <summary>
    /// Executes the destroy operation.
    /// </summary>
    /// <param name="arguments">The arguments controlling the teardown.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task Execute(DestroyArguments arguments, CancellationToken cancellationToken = default);
}
