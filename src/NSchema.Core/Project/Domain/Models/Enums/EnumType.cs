using System.Diagnostics;

namespace NSchema.Project.Domain.Models.Enums;

/// <summary>
/// Represents a database enum type: a named, ordered list of allowed string values.
/// </summary>
/// <param name="Name">The name of the enum type.</param>
/// <param name="Values">The allowed values, in order.</param>
/// <param name="OldName">The previous name of the enum type, if it has been renamed.</param>
/// <param name="Comment">An optional comment or description for the enum type.</param>
[DebuggerDisplay("{Name,nq} (enum)")]
public sealed record EnumType(
    SqlIdentifier Name,
    IReadOnlyList<string>? Values = null,
    SqlIdentifier? OldName = null,
    string? Comment = null
) : IRenameableObject
{
    /// <summary>
    /// The allowed values, in order.
    /// </summary>
    public IReadOnlyList<string> Values { get; init; } = Values ?? [];
}
