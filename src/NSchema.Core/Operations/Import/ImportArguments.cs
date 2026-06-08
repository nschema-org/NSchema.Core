using NSchema.Schema.Serialization;

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
    /// The file to write to when <see cref="Partition"/> is <see cref="ImportPartitionMode.None"/>.
    /// Ignored for the partitioned modes, which write into <see cref="OutputDirectory"/>.
    /// </summary>
    public string OutputFile { get; init; } = "schema.json";

    /// <summary>
    /// The root directory to write into when <see cref="Partition"/> is
    /// <see cref="ImportPartitionMode.Schema"/> or <see cref="ImportPartitionMode.Table"/>.
    /// Ignored for <see cref="ImportPartitionMode.None"/>, which writes to <see cref="OutputFile"/>.
    /// </summary>
    public string OutputDirectory { get; init; } = ".";

    /// <summary>
    /// Controls how the imported schema is split across output files.
    /// </summary>
    public ImportPartitionMode Partition { get; init; } = ImportPartitionMode.None;

    /// <summary>
    /// The serializer format key (e.g. <c>json</c>) to write with. Defaults to <c>json</c>.
    /// </summary>
    public string Format { get; init; } = JsonSchemaSerializer.FormatName;
}
