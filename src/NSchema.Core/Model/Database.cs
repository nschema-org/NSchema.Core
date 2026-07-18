using System.Diagnostics;
using NSchema.Model.Extensions;
using NSchema.Model.Schemas;

namespace NSchema.Model;

/// <summary>
/// Represents the overall structure of a database.
/// </summary>
[DebuggerDisplay("{Schemas.Count} schemas")]
public sealed class Database : IEquatable<Database>
{
    /// <summary>
    /// A list of Schema objects, each representing a specific schema within the database.
    /// </summary>
    public List<Schema> Schemas { get; init; } = [];

    /// <summary>
    /// A list of database-global extensions. Extensions are not schema-scoped, so they live at the root of the
    /// database schema rather than inside a <see cref="Schema"/>.
    /// </summary>
    public List<Extension> Extensions { get; init; } = [];

    /// <summary>
    /// The identity of everything the database contains: its schemas, their objects, and its extensions.
    /// </summary>
    public IdentitySet Identities() => new(
        [.. Schemas.Select(s => s.Name)],
        [.. Schemas.SelectMany(s => s.Objects().Select(o => o.Identity!))],
        [.. Extensions.Select(e => e.Name)]);

    /// <summary>
    /// Returns a deep copy of the database.
    /// </summary>
    public Database Clone() => new()
    {
        Schemas = [.. Schemas.Select(s => s.Clone())],
        Extensions = [.. Extensions.Select(e => e.Clone())],
    };

    /// <summary>
    /// Returns a copy of the database restricted to the schemas, objects, and extensions whose identity is in the set.
    /// </summary>
    public Database FilteredTo(IdentitySet identities) => new()
    {
        Schemas = [.. Schemas.Where(s => identities.ContainsSchema(s.Name)).Select(s => s.FilteredTo(identities))],
        Extensions = [.. Extensions.Where(identities.Contains).Select(e => e.Clone())],
    };

    /// <summary>
    /// Returns a new database model restricted to the current scope.
    /// </summary>
    public Database ScopedTo(PlanningScope scope)
    {
        if (scope.IsUnscoped)
        {
            return this;
        }

        // A targeted object still needs its container in the tree, even though the scope does not cover the
        // schema itself.
        var covered = Identities().CoveredBy(scope);
        return FilteredTo(covered with { Schemas = [.. covered.Schemas.Union(covered.Objects.Select(o => o.Schema))] });
    }

    /// <summary>
    /// Structural equality over the declared contents.
    /// </summary>
    public bool Equals(Database? other) =>
        other is not null
        && Schemas.SequenceEqual(other.Schemas)
        && Extensions.SequenceEqual(other.Extensions);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Database other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Schemas.Count, Extensions.Count);
}
