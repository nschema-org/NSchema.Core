using NSchema.Migration;

namespace NSchema.Hosting;

/// <summary>
/// Configures how a migration run is executed.
/// </summary>
public class MigrationRunOptions
{
    /// <summary>
    /// The operation the migration run performs. Defaults to <see cref="MigrationOperation.Plan"/>.
    /// </summary>
    public MigrationOperation Operation { get; set; } = MigrationOperation.Plan;

    /// <summary>
    /// Controls how exceptions are surfaced.
    /// </summary>
    public ExceptionBehavior ExceptionBehavior { get; set; } = ExceptionBehavior.ReportAndThrow;
}
