using NSchema.Schema.Model.Migrations;
using NSchema.Schema.Model.Scripts;

namespace NSchema.Schema.Model;

/// <summary>
/// The desired state read from the project's DDL.
/// </summary>
/// <param name="Schema">The aggregated desired schema.</param>
/// <param name="Scripts">The deployment scripts declared inline across the DDL files.</param>
/// <param name="Migrations">The data migrations declared inline across the DDL files.</param>
/// <param name="Files">The full paths of the DDL files the project was read from, in load order. Carried so the
/// application layer can surface "which files did it actually read" as verbose output without the domain layer
/// depending on the reporter.</param>
internal sealed record DesiredProject(
    DatabaseSchema Schema,
    IReadOnlyList<Script> Scripts,
    IReadOnlyList<DataMigration> Migrations,
    IReadOnlyList<string>? Files = null)
{
    /// <summary>
    /// The full paths of the DDL files the project was read from, in load order.
    /// </summary>
    public IReadOnlyList<string> Files { get; init; } = Files ?? [];
}
