using NSchema.Model;
namespace NSchema.Project.Domain.Models.Domains;

/// <summary>
/// The management directives declared for domains.
/// </summary>
/// <param name="Renames">The declared domain renames.</param>
/// <param name="Drops">The domains explicitly declared dropped.</param>
public sealed record DomainDirectives(
    IReadOnlyList<ObjectRename>? Renames = null,
    IReadOnlyList<ObjectReference>? Drops = null
)
{
    /// <summary>
    /// The declared domain renames.
    /// </summary>
    public IReadOnlyList<ObjectRename> Renames { get; init; } = Renames ?? [];

    /// <summary>
    /// The domains explicitly declared dropped.
    /// </summary>
    public IReadOnlyList<ObjectReference> Drops { get; init; } = Drops ?? [];
}
