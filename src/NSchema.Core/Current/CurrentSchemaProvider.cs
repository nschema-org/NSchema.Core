using NSchema.Current.Backends;
using NSchema.Current.Storage;
using NSchema.Current.Storage.Backends;
using NSchema.Project.Domain.Models;

namespace NSchema.Current;

/// <summary>
/// Wires the optional online (live database) and offline (persisted snapshot) sources together.
/// </summary>
/// <param name="serializer">Deserializes the state-store payload for offline reads.</param>
/// <param name="online">The live database provider, if any.</param>
/// <param name="store">The state store, if any.</param>
internal sealed class CurrentSchemaProvider(
    ISchemaStateSerializer serializer,
    ISchemaProvider? online = null,
    ISchemaStateStore? store = null
) : ICurrentSchemaProvider
{
    /// <inheritdoc />
    public ValueTask<DatabaseSchema> GetSchema(SchemaSourceMode preferred, string[]? schemaNames, bool required = true, CancellationToken cancellationToken = default)
    {
        var useOnline = preferred switch
        {
            SchemaSourceMode.Online when online is not null => true,
            SchemaSourceMode.Online when required => throw new InvalidOperationException("No online schema provider is registered."),
            SchemaSourceMode.Online when store is not null => false,
            SchemaSourceMode.Online => throw new InvalidOperationException("No schema provider is configured."),

            SchemaSourceMode.Offline when store is not null => false,
            SchemaSourceMode.Offline when required => throw new InvalidOperationException("No offline schema provider is registered."),
            SchemaSourceMode.Offline when online is not null => true,
            SchemaSourceMode.Offline => throw new InvalidOperationException("No schema provider is configured."),

            _ => throw new ArgumentOutOfRangeException(nameof(preferred), preferred, null),
        };

        return useOnline ? online!.GetSchema(schemaNames, cancellationToken) : ReadRecorded(schemaNames, cancellationToken);
    }

    /// <summary>
    /// Reads the recorded schema from the state store. When no state exists yet (bootstrap), an empty
    /// schema is returned so the first plan shows a full create; a scoped read filters to the requested
    /// schemas so the diff never plans to drop unmanaged ones.
    /// </summary>
    private async ValueTask<DatabaseSchema> ReadRecorded(string[]? schemaNames, CancellationToken cancellationToken)
    {
        var snapshot = await store!.Read(cancellationToken);
        if (snapshot is null)
        {
            return new DatabaseSchema();
        }

        return serializer.Deserialize(snapshot.Value).Schema.Filter(schemaNames);
    }
}
