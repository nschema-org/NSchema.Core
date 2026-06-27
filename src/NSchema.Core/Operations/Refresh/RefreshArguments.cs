namespace NSchema.Operations.Refresh;

/// <summary>
/// Arguments for an <see cref="IRefreshOperation"/> run.
/// </summary>
public sealed record RefreshArguments
{
    /// <summary>
    /// When <see langword="true"/>, the refresh runs without acquiring the state lock.
    /// </summary>
    public bool SkipLock { get; init; }
}
