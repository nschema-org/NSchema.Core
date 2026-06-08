namespace NSchema.Operations.ForceUnlock;

/// <summary>
/// Forcibly removes the state lock, for recovering from a stale lock.
/// </summary>
internal interface IForceUnlockOperation
{
    /// <summary>
    /// Executes the force-unlock operation.
    /// </summary>
    /// <param name="arguments">The arguments controlling the force-unlock.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task Execute(ForceUnlockArguments arguments, CancellationToken cancellationToken = default);
}
