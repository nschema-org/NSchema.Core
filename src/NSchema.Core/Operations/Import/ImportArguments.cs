namespace NSchema.Operations.Import;

/// <summary>
/// Arguments controlling which parts of the live schema an <see cref="IImportOperation"/> fetches.
/// </summary>
public sealed record ImportArguments
{
    /// <summary>
    /// The schema namespaces to import. When <see langword="null"/>, all namespaces are imported.
    /// </summary>
    public string[]? Schemas { get; init; }

    /// <summary>
    /// The table names to import. When <see langword="null"/>, all tables are imported.
    /// </summary>
    public string[]? Tables { get; init; }

    /// <summary>
    /// The name of the import target to write to.
    /// </summary>
    public string Target { get; set; } = "";
}
