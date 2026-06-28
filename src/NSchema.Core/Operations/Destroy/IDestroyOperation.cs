using NSchema.Diagnostics;

namespace NSchema.Operations.Destroy;

/// <summary>
/// Tears down the managed schema, dropping all managed objects from the target.
/// </summary>
internal interface IDestroyOperation
{
    /// <summary>
    /// Executes the destroy operation, returning success or a failure carrying the diagnostics that blocked it.
    /// </summary>
    /// <param name="arguments">The arguments controlling the teardown.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<Result> Execute(DestroyArguments arguments, CancellationToken cancellationToken = default);
}
