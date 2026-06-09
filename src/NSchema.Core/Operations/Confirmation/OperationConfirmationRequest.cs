namespace NSchema.Operations.Confirmation;

/// <summary>
/// Describes an operation awaiting user confirmation before performs an action the caller may want to review first.
/// </summary>
public abstract record OperationConfirmationRequest
{
    /// <summary>
    /// <see langword="true"/> when the action destroys or removes data.
    /// </summary>
    public virtual bool IsDestructive => false;
}
