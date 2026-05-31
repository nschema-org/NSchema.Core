namespace NSchema.Migration.Sql;

/// <summary>
/// Controls how the SQL executor runs the compiled migration plan.
/// </summary>
public class SqlExecutorOptions
{
    /// <summary>
    /// Controls whether the executor wraps the migration in a transaction.
    /// </summary>
    public TransactionMode TransactionMode { get; set; } = TransactionMode.Single;
}
