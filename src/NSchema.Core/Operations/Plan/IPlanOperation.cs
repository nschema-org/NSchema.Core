namespace NSchema.Operations.Plan;

/// <summary>
/// Computes and renders the migration plan without applying it to the target.
/// </summary>
internal interface IPlanOperation
{
    /// <summary>
    /// Executes the plan operation.
    /// </summary>
    /// <param name="arguments">The arguments controlling the plan.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<PlanResult> Execute(PlanArguments arguments, CancellationToken cancellationToken = default);
}
