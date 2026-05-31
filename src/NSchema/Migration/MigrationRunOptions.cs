namespace NSchema.Migration;

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
    /// Controls whether the executor wraps the migration in a transaction.
    /// </summary>
    public TransactionMode TransactionMode { get; set; } = TransactionMode.Single;
}
