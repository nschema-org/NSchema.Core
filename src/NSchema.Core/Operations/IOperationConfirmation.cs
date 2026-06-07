using NSchema.Plan.Model;

namespace NSchema.Operations;

/// <summary>
/// Decides whether an operation should be applied.
/// </summary>
public interface IOperationConfirmation
{
    /// <summary>
    /// Determines whether the operation described by <paramref name="plan"/> should be applied.
    /// </summary>
    /// <param name="plan">The computed migration plan awaiting confirmation.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> to proceed with the operation; <see langword="false"/> to abort.</returns>
    ValueTask<bool> Confirm(MigrationPlan plan, CancellationToken cancellationToken = default);
}
