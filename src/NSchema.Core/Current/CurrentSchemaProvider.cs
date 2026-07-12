using NSchema.Current.Backends;
using NSchema.Current.Storage;
using NSchema.Project.Domain;
using NSchema.Project.Domain.Models;

namespace NSchema.Current;

/// <summary>
/// Fetches the current state either offline from the store or online from the live database.
/// </summary>
/// <param name="stateManager">Reads the recorded state for offline reads.</param>
/// <param name="online">The live database provider, if any.</param>
internal sealed class CurrentSchemaProvider(ISchemaStateManager stateManager, ISchemaIntrospector? online = null) : ICurrentSchemaProvider
{
    /// <inheritdoc />
    public async Task<Result<DatabaseSchema>> GetSchema(SchemaSourceMode source, SchemaScope scope, CancellationToken cancellationToken = default)
    {
        switch (source)
        {
            case SchemaSourceMode.Online when online is null:
                return Result.Failure<DatabaseSchema>(CurrentDiagnostics.NoOnlineSource);
            case SchemaSourceMode.Online:
                // The introspector's scope is an optimization hint that may over-return, so the scope is
                // re-applied here — scoping semantics live in one place, whatever the provider did.
                var live = await online.GetSchema(scope, cancellationToken);
                return SchemaFilter.Apply(live, scope);

            case SchemaSourceMode.Offline when !stateManager.IsConfigured:
                return Result.Failure<DatabaseSchema>(CurrentDiagnostics.NoOfflineSource);
            case SchemaSourceMode.Offline:
                return await ReadRecorded(scope, cancellationToken);

            default:
                throw new ArgumentOutOfRangeException(nameof(source), source, null);
        }
    }

    /// <summary>
    /// Reads the recorded schema from the state store. When no state exists yet (bootstrap), an empty
    /// schema is returned so the first plan shows a full create; a scoped read filters to the requested
    /// schemas so the diff never plans to drop unmanaged ones.
    /// </summary>
    private async Task<Result<DatabaseSchema>> ReadRecorded(SchemaScope scope, CancellationToken cancellationToken)
    {
        var read = await stateManager.Read(new StateReadArguments(), cancellationToken);
        if (read.Value is not { } value)
        {
            return Result.Failure<DatabaseSchema>(read.Diagnostics);
        }

        return value.State is null
            ? new DatabaseSchema()
            : Result.From(SchemaFilter.Apply(value.State.Schema, scope), read.Diagnostics);
    }
}
