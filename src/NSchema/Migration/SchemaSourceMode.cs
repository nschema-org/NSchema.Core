namespace NSchema.Migration;

/// <summary>
/// Indicates which schema source to read when retrieving the current database state.
/// </summary>
public enum SchemaSourceMode
{
    /// <summary>
    /// Read the live database. Required for an apply; captures real state after migration.
    /// </summary>
    Online,

    /// <summary>
    /// Read the persisted state snapshot. Used for offline planning without a database connection.
    /// </summary>
    Offline,
}