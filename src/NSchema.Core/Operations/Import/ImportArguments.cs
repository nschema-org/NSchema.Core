namespace NSchema.Operations.Import;

/// <summary>
/// Arguments controlling what an <see cref="IImportOperation"/> fetches from the live database and where it writes the result.
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
    /// The root directory to write the imported schema into. Defaults to the current directory.
    /// </summary>
    public string OutputDirectory { get; init; } = ".";
}
