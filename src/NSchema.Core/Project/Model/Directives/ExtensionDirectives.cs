using NSchema.Model;

namespace NSchema.Project.Domain.Models;

/// <summary>
/// The management directives declared for database-global extensions.
/// </summary>
/// <param name="Drops">The extensions explicitly declared dropped.</param>
public sealed record ExtensionDirectives(IReadOnlyList<SqlIdentifier>? Drops = null)
{
    /// <summary>
    /// The extensions explicitly declared dropped.
    /// </summary>
    public IReadOnlyList<SqlIdentifier> Drops { get; init; } = Drops ?? [];
}
