using NSchema.Schema.Serialization;

namespace NSchema.Import;

/// <summary>
/// Options for <see cref="FileSchemaImportTarget"/>.
/// </summary>
public class FileSchemaImportTargetOptions
{
    /// <summary>
    /// The output path. For <see cref="ImportPartitionMode.None"/> this is the file path;
    /// for <see cref="ImportPartitionMode.Schema"/> and <see cref="ImportPartitionMode.Table"/>
    /// this is the root directory.
    /// </summary>
    public string OutputPath { get; set; } = ".";

    /// <summary>
    /// The serializer format key (e.g. <c>json</c>). Defaults to <c>json</c>.
    /// </summary>
    public string Format { get; set; } = JsonSchemaDocumentSerializer.FormatName;

    /// <summary>
    /// Controls how the imported schema is split across output files.
    /// </summary>
    public ImportPartitionMode Partition { get; set; } = ImportPartitionMode.None;
}
