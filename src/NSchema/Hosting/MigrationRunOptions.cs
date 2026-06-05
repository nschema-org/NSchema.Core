using NSchema.Migration;

namespace NSchema.Hosting;

/// <summary>
/// Configures how a migration run is executed.
/// </summary>
public class MigrationRunOptions
{
    /// <summary>
    /// The default output format: human-readable terminal output.
    /// </summary>
    public const string DefaultOutputFormat = "human";

    /// <summary>
    /// The operation the migration run performs. Defaults to <see cref="MigrationOperation.Plan"/>.
    /// </summary>
    public MigrationOperation Operation { get; set; } = MigrationOperation.Plan;

    /// <summary>
    /// The format used to render run output, resolved to an <see cref="IMigrationReporter"/> at runtime.
    /// </summary>
    public string OutputFormat { get; set; } = DefaultOutputFormat;

    /// <summary>
    /// The SQL dialect to generate, resolved to an <see cref="Sql.ISqlGenerator"/> at runtime.
    /// </summary>
    public string Dialect { get; set; } = "";

    /// <summary>
    /// Controls how exceptions are surfaced.
    /// </summary>
    public ExceptionBehavior ExceptionBehavior { get; set; } = ExceptionBehavior.ReportAndThrow;
}
