using NSchema.Diagnostics;

namespace NSchema.Operations.Plan;

/// <summary>
/// Computes the migration plan without applying it to the target.
/// </summary>
internal interface IPlanOperation
{
    /// <summary>
    /// Executes the plan operation, returning the result.
    /// </summary>
    /// <param name="arguments">The arguments controlling the plan.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<Result<PlanResult>> Execute(PlanArguments arguments, CancellationToken cancellationToken = default);
}
