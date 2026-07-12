using System.Diagnostics;

namespace NSchema.Project.Domain.Models.CompositeTypes;

/// <summary>
/// Represents a database composite type: a schema-scoped named tuple of typed <see cref="Fields"/>.
/// </summary>
/// <param name="Name">The name of the composite type.</param>
/// <param name="Fields">The ordered fields (attributes) of the type; may be empty.</param>
/// <param name="OldName">The previous name of the composite type, if it has been renamed.</param>
/// <param name="Comment">An optional comment or description for the composite type.</param>
[DebuggerDisplay("{Name,nq} (composite type, {Fields.Count} fields)")]
public sealed record CompositeType(
    string Name,
    IReadOnlyList<CompositeField>? Fields = null,
    string? OldName = null,
    string? Comment = null
) : IRenameableObject
{
    /// <summary>
    /// The fields (attributes) of the type, matched by name; may be empty.
    /// </summary>
    public IReadOnlyList<CompositeField> Fields { get; init; } = Fields ?? [];
}
