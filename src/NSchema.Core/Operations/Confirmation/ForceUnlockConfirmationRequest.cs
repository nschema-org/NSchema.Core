namespace NSchema.Operations.Confirmation;

/// <summary>
/// A request to confirm a force-unlock, which removes the state lock regardless of who holds it.
/// </summary>
public sealed record ForceUnlockConfirmationRequest : OperationConfirmationRequest
{
    /// <inheritdoc />
    public override bool IsDestructive => true;
}
