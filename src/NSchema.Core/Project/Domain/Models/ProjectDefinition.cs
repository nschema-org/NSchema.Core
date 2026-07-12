using NSchema.Project.Domain.Models.Scripts;

namespace NSchema.Project.Domain.Models;

/// <summary>
/// The desired state declared by the project's DDL.
/// </summary>
/// <param name="Schema">The aggregated desired schema.</param>
/// <param name="Scripts">The scripts declared inline across the DDL files.</param>
public sealed record ProjectDefinition(DatabaseSchema Schema, IReadOnlyList<Script> Scripts);
