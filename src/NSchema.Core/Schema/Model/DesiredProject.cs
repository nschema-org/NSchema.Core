using NSchema.Schema.Model.Migrations;
using NSchema.Schema.Model.Scripts;

namespace NSchema.Schema.Model;

/// <summary>
/// The desired state declared by the project's DDL.
/// </summary>
/// <param name="Schema">The aggregated desired schema.</param>
/// <param name="Scripts">The deployment scripts declared inline across the DDL files.</param>
/// <param name="Migrations">The data migrations declared inline across the DDL files.</param>
internal sealed record DesiredProject(
    DatabaseSchema Schema,
    IReadOnlyList<Script> Scripts,
    IReadOnlyList<DataMigration> Migrations
);
