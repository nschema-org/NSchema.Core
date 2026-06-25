namespace NSchema.Operations.Doctor;

/// <summary>
/// Runs read-only health checks against the configured infrastructure and reports the outcome of each.
/// </summary>
internal interface IDoctorOperation
{
    Task Execute(DoctorArguments arguments, CancellationToken cancellationToken = default);
}
