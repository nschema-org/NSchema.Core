using NSchema.Plan.Model;

namespace NSchema.Operations.Confirmation;

/// <summary>
/// A request to confirm an apply, which applies the plan to the database.
/// </summary>
/// <param name="Plan">The computed migration plan awaiting confirmation.</param>
public sealed record ApplyConfirmationRequest(MigrationPlan Plan) : OperationConfirmationRequest;
