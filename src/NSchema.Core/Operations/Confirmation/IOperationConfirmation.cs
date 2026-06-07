namespace NSchema.Operations.Confirmation;

/// <summary>
/// Decides whether an operation should proceed.
/// </summary>
public interface IOperationConfirmation
{
    /// <summary>
    /// Determines whether the operation described by <paramref name="request"/> should proceed.
    /// </summary>
    /// <param name="request">Describes the operation awaiting confirmation, including its plan and whether it is destructive.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> to proceed with the operation; <see langword="false"/> to abort.</returns>
    ValueTask<bool> Confirm(OperationConfirmationRequest request, CancellationToken cancellationToken = default);
}
