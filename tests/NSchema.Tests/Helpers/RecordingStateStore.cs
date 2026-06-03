using NSchema.Schema.Model;
using NSchema.State;

namespace NSchema.Tests.Helpers;

/// <summary>
/// In-memory <see cref="ISchemaStateStore"/> that captures what was written. Used in place of the file store
/// when a test needs to assert on the persisted schema after the host has disposed its service provider.
/// </summary>
internal sealed class RecordingStateStore : ISchemaStateStore
{
    public DatabaseSchema? Written { get; private set; }

    public Task<DatabaseSchema?> Read(CancellationToken cancellationToken = default) => Task.FromResult(Written);

    public Task Write(DatabaseSchema schema, CancellationToken cancellationToken = default)
    {
        Written = schema;
        return Task.CompletedTask;
    }
}
