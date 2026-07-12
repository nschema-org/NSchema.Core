using NSchema.Current.Storage;
using NSchema.Current.Storage.Backends;
using NSchema.Current.Domain.Models;

namespace NSchema.Tests.Helpers;

/// <summary>
/// In-memory <see cref="ISchemaStateStore"/> that captures what was written. Used in place of the file store
/// when a test needs to assert on the persisted schema after the host has disposed its service provider.
/// </summary>
internal sealed class RecordingStateStore : ISchemaStateStore
{
    private static readonly SchemaStateSerializer _serializer = new();

    public SchemaState? Written { get; private set; }

    // The explicit nullable default matters: a bare `null` here would convert through byte[] to an
    // empty (non-null) ReadOnlyMemory, which reads as a corrupt zero-byte payload rather than "no state".
    public Task<ReadOnlyMemory<byte>?> Read(CancellationToken cancellationToken = default) =>
        Task.FromResult(Written is null ? default(ReadOnlyMemory<byte>?) : _serializer.Serialize(Written));

    public Task Write(ReadOnlyMemory<byte> state, CancellationToken cancellationToken = default)
    {
        Written = _serializer.Deserialize(state);
        return Task.CompletedTask;
    }
}
