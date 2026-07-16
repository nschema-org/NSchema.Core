using NSchema.Deployment.Backends;
using NSchema.Model;

namespace NSchema.Deployment;

/// <summary>
/// Fetches the live database from the registered introspector.
/// </summary>
/// <param name="online">The live database provider, if any.</param>
internal sealed class DatabaseProvider(IDatabaseIntrospector? online = null) : IDatabaseProvider
{
    /// <inheritdoc />
    public async Task<Result<Database>> GetDatabase(PlanningScope scope, CancellationToken cancellationToken = default)
    {
        if (online is null)
        {
            return Result.Failure<Database>(DeploymentDiagnostics.NoOnlineSource);
        }

        // The introspector's scope is an optimization hint that may over-return, so the scope is
        // re-applied here — scoping semantics live in one place, whatever the provider did.
        var live = await online.GetDatabase(scope, cancellationToken);
        return live.ScopedTo(scope);
    }
}
