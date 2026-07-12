namespace NSchema.Current.Locks;

/// <summary>
/// Describes an operation acquiring the state lock.
/// </summary>
/// <param name="Operation">The name of the operation acquiring the lock (e.g. <c>"apply"</c>, <c>"destroy"</c>, <c>"refresh"</c>).</param>
/// <param name="TimeToLive">An optional lifetime after which the lock is considered stale.</param>
public sealed record StateLockRequest(string Operation, TimeSpan? TimeToLive = null);
