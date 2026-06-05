namespace NSchema.Import;

/// <summary>
/// Controls which parts of the live schema are fetched during an import operation.
/// </summary>
public class ImportOptions
{
    /// <summary>
    /// The schema namespaces to import. When <see langword="null"/>, all namespaces are imported.
    /// </summary>
    public string[]? Schemas { get; set; }

    /// <summary>
    /// The table names to import. When <see langword="null"/>, all tables are imported.
    /// </summary>
    public string[]? Tables { get; set; }

    /// <summary>
    /// The name of the import target to write to.
    /// </summary>
    public string Target { get; set; } = FileSchemaImportTarget.TargetName;
}
