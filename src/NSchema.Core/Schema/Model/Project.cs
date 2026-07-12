using NSchema.Schema.Model.Scripts;

namespace NSchema.Schema.Model;

/// <summary>
/// The desired state declared by the project's DDL.
/// </summary>
/// <param name="Schema">The aggregated desired schema.</param>
/// <param name="Scripts">The scripts declared inline across the DDL files.</param>
public sealed record Project(DatabaseSchema Schema, IReadOnlyList<Script> Scripts);
