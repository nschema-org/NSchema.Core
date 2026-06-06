namespace NSchema.Import;

/// <summary>
/// Controls how an import is split across output files.
/// </summary>
public enum ImportPartitionMode
{
    /// <summary>
    /// The entire imported schema is written to a single file.
    /// </summary>
    None,

    /// <summary>
    /// One file is written per schema namespace.
    /// </summary>
    Schema,

    /// <summary>
    /// One file is written per table.
    /// </summary>
    Table,
}
