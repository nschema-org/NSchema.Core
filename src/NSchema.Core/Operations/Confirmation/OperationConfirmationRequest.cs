using NSchema.Plan.Model;

namespace NSchema.Operations.Confirmation;

/// <summary>
/// Describes an operation awaiting user confirmation before it makes changes to the database.
/// </summary>
/// <param name="Plan">The computed migration plan awaiting confirmation.</param>
public abstract record OperationConfirmationRequest(MigrationPlan Plan)
{
    /// <summary>
    /// <see langword="true"/> when the action drops managed objects (e.g. a teardown).
    /// </summary>
    public virtual bool IsDestructive => false;
}
