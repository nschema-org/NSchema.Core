using NSchema.Diagnostics;

namespace NSchema.Operations.Refresh;

/// <summary>
/// Reads the live current schema and writes it to the state store, without planning or applying anything.
/// </summary>
internal interface IRefreshOperation
{
    /// <summary>
    /// Executes the refresh operation.
    /// </summary>
    /// <param name="arguments">The arguments controlling the refresh.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<Result> Execute(RefreshArguments arguments, CancellationToken cancellationToken = default);
}
