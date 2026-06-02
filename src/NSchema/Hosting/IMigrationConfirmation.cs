using NSchema.Migration.Plan;

namespace NSchema.Hosting;

/// <summary>
/// Decides whether a computed migration should be applied.
/// </summary>
public interface IMigrationConfirmation
{
    /// <summary>
    /// Determines whether the migration described by <paramref name="plan"/> should be applied.
    /// </summary>
    /// <param name="plan">The computed migration plan awaiting confirmation.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> to proceed with the migration; <see langword="false"/> to abort without making changes.</returns>
    ValueTask<bool> Confirm(MigrationPlan plan, CancellationToken cancellationToken = default);
}
