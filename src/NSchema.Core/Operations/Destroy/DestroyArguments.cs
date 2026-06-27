namespace NSchema.Operations.Destroy;

/// <summary>
/// Arguments for an <see cref="IDestroyOperation"/> run.
/// </summary>
public sealed record DestroyArguments
{
    /// <summary>
    /// When <see langword="true"/>, the teardown runs without acquiring the state lock.
    /// </summary>
    public bool SkipLock { get; init; }
}
