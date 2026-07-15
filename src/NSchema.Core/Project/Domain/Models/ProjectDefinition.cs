using NSchema.Model;
namespace NSchema.Project.Domain.Models;

/// <summary>
/// The desired state declared by the project.
/// </summary>
/// <param name="Database">The declared database structure.</param>
/// <param name="Directives">The management directives declared across the project's files.</param>
public sealed record ProjectDefinition(Database Database, ProjectDirectives? Directives = null)
{
    /// <summary>
    /// The management directives declared across the project's files.
    /// </summary>
    public ProjectDirectives Directives { get; init; } = Directives ?? ProjectDirectives.Empty;

    /// <summary>
    /// Every schema name the project manages.
    /// </summary>
    public SqlIdentifier[] ManagedSchemaNames => Database.Schemas
        .Select(s => s.Name)
        .Concat(Directives.Schemas.Drops)
        .Concat(Directives.Schemas.Renames.Select(r => r.From))
        .Distinct()
        .ToArray();
}
