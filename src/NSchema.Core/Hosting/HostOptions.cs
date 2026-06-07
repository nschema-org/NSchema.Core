namespace NSchema.Hosting;

/// <summary>
/// Configures how the host is executed.
/// </summary>
internal class HostOptions
{
    /// <summary>
    /// The operation to perform. Defaults to <see cref="HostOperation.Plan"/>.
    /// </summary>
    public HostOperation Operation { get; set; } = HostOperation.Plan;

    /// <summary>
    /// Controls how exceptions are surfaced.
    /// </summary>
    public ExceptionBehavior ExceptionBehavior { get; set; } = ExceptionBehavior.ReportAndThrow;
}
