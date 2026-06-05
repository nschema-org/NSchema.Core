using NSchema.Scripts.Model;

namespace NSchema.Scripts;

/// <summary>
/// Provides scripts to run as part of a migration plan.
/// </summary>
public interface IScriptProvider
{
    /// <summary>
    /// Gets the scripts to be run during the migration plan.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The scripts to run.</returns>
    ValueTask<IReadOnlyList<Script>> GetScripts(CancellationToken cancellationToken = default);
}
