using System.Diagnostics.CodeAnalysis;

namespace NSchema.Project.Domain.Models;

/// <summary>
/// The coarse source-read scope: which schemas are under management for this run. A scope applies
/// symmetrically to every source read, so both sides of a diff cover the same schemas.
/// </summary>
public sealed record SchemaScope
{
    private SchemaScope(IReadOnlyList<SqlIdentifier>? schemaNames)
    {
        SchemaNames = schemaNames;
    }

    /// <summary>
    /// Every schema the source can see.
    /// </summary>
    public static SchemaScope All { get; } = new((IReadOnlyList<SqlIdentifier>?)null);

    /// <summary>
    /// A scope restricted to the named schemas (case-insensitive). An empty set normalizes to <see cref="All"/>.
    /// </summary>
    /// <param name="schemaNames">The schema names under management.</param>
    public static SchemaScope Of(params IEnumerable<SqlIdentifier> schemaNames)
    {
        var names = schemaNames.ToList();
        return names.Count == 0 ? All : new SchemaScope(names);
    }

    /// <summary>
    /// The schema names under management, or <see langword="null"/> when the scope is <see cref="All"/>.
    /// </summary>
    public IReadOnlyList<SqlIdentifier>? SchemaNames { get; }

    /// <summary>
    /// Whether this scope includes every schema.
    /// </summary>
    [MemberNotNullWhen(false, nameof(SchemaNames))]
    public bool IsAll => SchemaNames is null;

    /// <summary>
    /// Whether the named schema is inside this scope.
    /// </summary>
    public bool Includes(SqlIdentifier schemaName) =>
        IsAll || SchemaNames.Contains(schemaName);
}
