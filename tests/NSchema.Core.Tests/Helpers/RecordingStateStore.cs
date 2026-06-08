using NSchema.Schema.Model;
using NSchema.State;

namespace NSchema.Tests.Helpers;

/// <summary>
/// In-memory <see cref="ISchemaStateStore"/> that captures what was written. Used in place of the file store
/// when a test needs to assert on the persisted schema after the host has disposed its service provider.
/// </summary>
internal sealed class RecordingStateStore : ISchemaStateStore
{
    private static readonly DefaultSchemaStateSerializer _serializer = new();

    public DatabaseSchema? Written { get; private set; }

    public Task<string?> Read(CancellationToken cancellationToken = default) =>
        Task.FromResult(Written is null ? null : _serializer.Serialize(Written));

    public Task Write(string state, CancellationToken cancellationToken = default)
    {
        Written = _serializer.Deserialize(state);
        return Task.CompletedTask;
    }
}
