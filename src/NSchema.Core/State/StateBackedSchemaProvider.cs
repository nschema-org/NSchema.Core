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
/// <param name="store">The state store to read the payload from.</param>
/// <param name="serializer">Deserializes the stored payload into a schema snapshot.</param>
internal sealed class StateBackedSchemaProvider(ISchemaStateStore store, ISchemaStateSerializer serializer) : ISchemaProvider
{
    /// <inheritdoc />
    public async ValueTask<DatabaseSchema> GetSchema(string[]? schemaNames = null, CancellationToken cancellationToken = default)
    {
        var snapshot = await store.Read(cancellationToken);
        // Ensure we return an empty schema for a bootstrap run.
        return snapshot is null ? DatabaseSchema.Create([]) : serializer.Deserialize(snapshot.Value).Filter(schemaNames);
    }
}
