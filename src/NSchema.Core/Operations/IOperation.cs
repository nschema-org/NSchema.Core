namespace NSchema.Operations;

/// <summary>
/// Represents a single operation (plan, apply, refresh, etc.).
/// </summary>
public interface IOperation
{
    /// <summary>
    /// Executes the operation.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task Execute(CancellationToken cancellationToken = default);
}
