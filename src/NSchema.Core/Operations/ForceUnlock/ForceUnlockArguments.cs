namespace NSchema.Operations.ForceUnlock;

/// <summary>
/// Arguments for an <see cref="IForceUnlockOperation"/> run.
/// </summary>
public sealed record ForceUnlockArguments
{
    /// <summary>
    /// When set, the force-unlock only proceeds if this matches the id of the lock currently held; otherwise it is
    /// refused. When <see langword="null"/>, whatever lock is held is removed.
    /// </summary>
    public string? ExpectedLockId { get; init; }
}
