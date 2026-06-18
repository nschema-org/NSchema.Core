namespace NSchema.Sql;

/// <summary>
/// Configures SQL options.
/// </summary>
public class SqlOptions
{
    /// <summary>
    /// Controls whether the migration runs inside a transaction.
    /// </summary>
    public TransactionMode TransactionMode { get; set; } = TransactionMode.Single;
}
