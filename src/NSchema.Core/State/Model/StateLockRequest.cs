namespace NSchema.State.Model;

/// <summary>
/// Describes an operation acquiring the state lock.
/// </summary>
/// <param name="Operation">The name of the operation acquiring the lock (e.g. <c>"apply"</c>, <c>"destroy"</c>, <c>"refresh"</c>).</param>
public sealed record StateLockRequest(string Operation);
