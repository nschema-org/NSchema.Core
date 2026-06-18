using System.Diagnostics;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.Constraints;

namespace NSchema.Schema.Model.Domains;

/// <summary>
/// Represents a database domain: a schema-scoped named type built on a base type.
/// </summary>
/// <param name="Name">The name of the domain.</param>
/// <param name="DataType">The underlying base type (e.g. <c>text</c>). It cannot be altered in place, so a change to it is planned as a drop + recreate.</param>
/// <param name="Default">An optional default expression, stored verbatim (opaque SQL); <see langword="null"/> when none.</param>
/// <param name="NotNull">Whether the domain forbids <c>NULL</c>.</param>
/// <param name="Checks">The domain's <c>CHECK</c> constraints (their expressions reference the domain's <c>VALUE</c>); empty when none.</param>
/// <param name="OldName">The previous name of the domain, if it has been renamed.</param>
/// <param name="Comment">An optional comment or description for the domain.</param>
[DebuggerDisplay("{Name,nq} (domain)")]
public sealed record Domain(
    string Name,
    SqlType DataType,
    string? Default = null,
    bool NotNull = false,
    IReadOnlyList<CheckConstraint>? Checks = null,
    string? OldName = null,
    string? Comment = null
) : IRenameableObject
{
    /// <summary>
    /// The domain's <c>CHECK</c> constraints; empty when none.
    /// </summary>
    public IReadOnlyList<CheckConstraint> Checks { get; init; } = Checks ?? [];
}
