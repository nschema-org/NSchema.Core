namespace NSchema.Operations.Apply;

/// <summary>
/// Computes the migration plan and applies it to the target.
/// </summary>
internal interface IApplyOperation
{
    /// <summary>
    /// Executes the apply operation.
    /// </summary>
    /// <param name="arguments">The arguments controlling the apply.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task Execute(ApplyArguments arguments, CancellationToken cancellationToken = default);
}
