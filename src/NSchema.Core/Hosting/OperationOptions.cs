using NSchema.Migration;

namespace NSchema.Hosting;

/// <summary>
/// Configures how an operation is executed.
/// </summary>
public class OperationOptions
{
    /// <summary>
    /// The default output format: human-readable terminal output.
    /// </summary>
    public const string DefaultOutputFormat = DefaultMigrationReporter.FormatName;

    /// <summary>
    /// The operation to perform. Defaults to <see cref="MigrationOperation.Plan"/>.
    /// </summary>
    public MigrationOperation Operation { get; set; } = MigrationOperation.Plan;

    /// <summary>
    /// The format used to render output, resolved to an <see cref="IMigrationReporter"/> at runtime.
    /// </summary>
    public string OutputFormat { get; set; } = DefaultOutputFormat;

    /// <summary>
    /// The SQL dialect to generate, resolved to an <see cref="Sql.ISqlGenerator"/> at runtime.
    /// </summary>
    public string? Dialect { get; set; }

    /// <summary>
    /// Controls whether the migration runs inside a transaction. Applies to <see cref="MigrationOperation.Apply"/> only.
    /// </summary>
    public TransactionMode TransactionMode { get; set; } = TransactionMode.Single;

    /// <summary>
    /// Controls how exceptions are surfaced.
    /// </summary>
    public ExceptionBehavior ExceptionBehavior { get; set; } = ExceptionBehavior.ReportAndThrow;
}
