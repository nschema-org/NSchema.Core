using NSchema.Schema;
using NSchema.Schema.Model;

namespace NSchema.State;

/// <summary>
/// A current-state <see cref="ISchemaProvider"/> backed by an <see cref="ISchemaStateStore"/>.
/// </summary>
/// <remarks>
/// When no state exists yet (bootstrap), an empty <see cref="DatabaseSchema"/> is returned
/// so the first plan shows a full create.
/// </remarks>
/// <param name="store">The state store to read the snapshot from.</param>
internal sealed class StateBackedSchemaProvider(ISchemaStateStore store) : ISchemaProvider
{
    /// <inheritdoc />
    public async ValueTask<DatabaseSchema> GetSchema(string[]? schemaNames = null, CancellationToken cancellationToken = default)
    {
        var schema = await store.Read(cancellationToken);
        // Ensure we return an empty schema for a bootstrap run.
        return schema?.Filter(schemaNames) ?? DatabaseSchema.Create([]);
    }
}
