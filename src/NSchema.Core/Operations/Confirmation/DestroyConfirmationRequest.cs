using NSchema.Plan.Model;

namespace NSchema.Operations.Confirmation;

/// <summary>
/// A request to confirm a destroy, which drops the managed schema objects from the database.
/// </summary>
/// <param name="Plan">The computed teardown plan awaiting confirmation.</param>
public sealed record DestroyConfirmationRequest(MigrationPlan Plan) : OperationConfirmationRequest
{
    /// <inheritdoc />
    public override bool IsDestructive => true;
}
