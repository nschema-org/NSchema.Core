namespace NSchema.Sql;

/// <summary>
/// Configures SQL options.
/// </summary>
public class SqlOptions
{
    /// <summary>
    /// The SQL dialect to generate, resolved to an <see cref="ISqlGenerator"/> at runtime.
    /// </summary>
    public string? Dialect { get; set; }

    /// <summary>
    /// Controls whether the migration runs inside a transaction.
    /// </summary>
    public TransactionMode TransactionMode { get; set; } = TransactionMode.Single;
}
