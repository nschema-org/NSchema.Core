using Microsoft.Extensions.DependencyInjection;
using NSchema.Schema;
using NSchema.Schema.Model;

namespace NSchema.State;

/// <summary>
/// Wires the optional online (live database) and offline (persisted snapshot) sources together.
/// </summary>
/// <param name="serializer">Deserializes the state-store payload for offline reads.</param>
/// <param name="online">The live database provider, if any.</param>
/// <param name="store">The state store, if any.</param>
internal sealed class CurrentSchemaProvider(
    ISchemaStateSerializer serializer,
    [FromKeyedServices(NSchemaKeys.OnlineSchemaProvider)]
    ISchemaProvider? online = null,
    ISchemaStateStore? store = null
) : ICurrentSchemaProvider
{
    private readonly StateBackedSchemaProvider? _offline = store is not null ? new StateBackedSchemaProvider(store, serializer) : null;

    /// <inheritdoc />
    public ValueTask<DatabaseSchema> GetSchema(SchemaSourceMode preferred, string[]? schemaNames, bool required = true, CancellationToken cancellationToken = default)
    {
        var provider = GetProvider(preferred, required);
        return provider.GetSchema(schemaNames, cancellationToken);
    }

    private ISchemaProvider GetProvider(SchemaSourceMode preferred, bool required = true) => preferred switch
    {
        SchemaSourceMode.Online when online is not null => online,
        SchemaSourceMode.Online when required => throw new InvalidOperationException("No online schema provider is registered."),
        SchemaSourceMode.Online => _offline ?? throw new InvalidOperationException("No schema provider is configured."),

        SchemaSourceMode.Offline when _offline is not null => _offline,
        SchemaSourceMode.Offline when required => throw new InvalidOperationException("No offline schema provider is registered. "),
        SchemaSourceMode.Offline => online ?? throw new InvalidOperationException("No schema provider is configured."),

        _ => throw new ArgumentOutOfRangeException(nameof(preferred), preferred, null),
    };
}
