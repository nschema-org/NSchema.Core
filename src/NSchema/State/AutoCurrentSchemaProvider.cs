using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSchema.Migration;
using NSchema.Schema;

namespace NSchema.State;

/// <summary>
/// A current-state <see cref="ISchemaProvider"/> that routes by operation: a <see cref="MigrationOperation.Plan"/>
/// reads the persisted state (offline), while an <see cref="MigrationOperation.Apply"/> reads the live database.
/// </summary>
/// <remarks>
/// This lets a single registration serve both operations — plan offline against the last applied state, then
/// apply against (and capture) the real database — without choosing the source per run.
/// </remarks>
/// <param name="liveProvider">
/// The live current-state provider, used for an apply. May be <see langword="null"/> when no live provider is
/// registered — e.g. a plan run in a PR preview where there is no database connection. A <see cref="MigrationOperation.Apply"/>
/// then has no source and throws.
/// </param>
/// <param name="store">The state store the plan-time snapshot is read from.</param>
/// <param name="options">Supplies the operation selected for the current run.</param>
internal sealed class AutoCurrentSchemaProvider(
    IOptions<MigrationOptions> options,
    ISchemaStateStore store,
    [FromKeyedServices(ISchemaProvider.LiveCurrentSchemaProviderKey)]
    ISchemaProvider? liveProvider = null
) : ISchemaProvider
{
    private readonly StateBackedSchemaProvider _stateProvider = new(store);

    /// <inheritdoc />
    public Task<DatabaseSchema> GetSchema(string[]? schemaNames = null, CancellationToken cancellationToken = default)
    {
        if (options.Value.Operation == MigrationOperation.Plan)
        {
            return _stateProvider.GetSchema(schemaNames, cancellationToken);
        }

        if (liveProvider is null)
        {
            throw new InvalidOperationException("An apply requires a live current-state provider, but none is registered.");
        }

        return liveProvider.GetSchema(schemaNames, cancellationToken);
    }
}
