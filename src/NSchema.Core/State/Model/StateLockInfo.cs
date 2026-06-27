namespace NSchema.State.Model;

/// <summary>
/// Metadata about a held state lock, recorded by the lock implementation so a blocked operation can report who
/// holds the lock and since when.
/// </summary>
/// <param name="Id">A unique identifier for the lock.</param>
/// <param name="Operation">The operation that acquired the lock (from <see cref="StateLockRequest.Operation"/>).</param>
/// <param name="Who">A human-readable identifier of who holds the lock (e.g. <c>user@machine</c>).</param>
/// <param name="CreatedUtc">When the lock was acquired.</param>
/// <param name="ExpiresUtc">When the lock is considered stale, or <see langword="null"/> when it has no expiry.</param>
public sealed record StateLockInfo(
    string Id,
    string Operation,
    string Who,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? ExpiresUtc = null
);
