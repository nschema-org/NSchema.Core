namespace NSchema.Operations.Destroy;

/// <summary>
/// Arguments for a teardown run (see <see cref="INSchemaOperations.BeginDestroy"/>).
/// </summary>
public sealed record DestroyArguments
{
    /// <summary>
    /// When <see langword="true"/>, the teardown runs without acquiring the state lock.
    /// </summary>
    public bool SkipLock { get; init; }
}
