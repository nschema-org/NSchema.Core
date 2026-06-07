using NSchema.Operations;

namespace NSchema.Hosting;

/// <summary>
/// Configures how the host is executed.
/// </summary>
internal class HostOptions
{
    /// <summary>
    /// The operation to perform. Defaults to <see cref="Operations.Operation.Plan"/>.
    /// </summary>
    public Operation Operation { get; set; } = Operation.Plan;

    /// <summary>
    /// Controls how exceptions are surfaced.
    /// </summary>
    public ExceptionBehavior ExceptionBehavior { get; set; } = ExceptionBehavior.ReportAndThrow;
}
