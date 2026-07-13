using NSchema.Project.Domain.Models;

namespace NSchema.Operations;

/// <summary>
/// Arguments controlling what an import fetches from the live database and where it writes the result.
/// </summary>
public sealed record ImportArguments
{
    /// <summary>
    /// The schema namespaces to import. When <see langword="null"/>, all namespaces are imported.
    /// </summary>
    public SchemaScope Scope { get; init; } = SchemaScope.All;

    /// <summary>
    /// The root directory to write the imported schema into. Defaults to the current directory.
    /// </summary>
    public string OutputDirectory { get; init; } = ".";
}
