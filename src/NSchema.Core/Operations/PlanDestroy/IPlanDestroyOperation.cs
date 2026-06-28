using NSchema.Diagnostics;
using NSchema.Operations.Plan;

namespace NSchema.Operations.PlanDestroy;

/// <summary>
/// Computes the teardown plan (the plan to drop the managed schema) without applying it to the target.
/// </summary>
internal interface IPlanDestroyOperation
{
    /// <summary>
    /// Executes the plan-destroy operation, returning the teardown result.
    /// </summary>
    /// <param name="arguments">The arguments controlling the teardown plan.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<Result<PlanResult>> Execute(PlanDestroyArguments arguments, CancellationToken cancellationToken = default);
}
