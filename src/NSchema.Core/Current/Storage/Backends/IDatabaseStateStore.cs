namespace NSchema.Current.Storage.Backends;

/// <summary>
/// Persists and retrieves a serialized snapshot of the current database schema, so migration plans can be computed offline.
/// </summary>
public interface ISchemaStateStore
{
    /// <summary>
    /// Reads the persisted schema snapshot.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The persisted snapshot, or <see langword="null"/> if no snapshot exists yet (bootstrap).</returns>
    Task<ReadOnlyMemory<byte>?> Read(CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a schema snapshot, replacing any existing state.
    /// </summary>
    /// <param name="state">The serialized snapshot to persist.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task Write(ReadOnlyMemory<byte> state, CancellationToken cancellationToken = default);
}
