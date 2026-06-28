using NSchema.Diagnostics;

namespace NSchema.Operations.Doctor;

/// <summary>
/// Runs read-only health checks against the configured infrastructure and reports the outcome of each.
/// </summary>
internal interface IDoctorOperation
{
    /// <summary>
    /// Runs every check and returns the aggregated diagnostics — a failure when any check found an error.
    /// </summary>
    Task<Result> Execute(DoctorArguments arguments, CancellationToken cancellationToken = default);
}
