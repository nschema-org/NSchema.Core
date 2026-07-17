using System.Diagnostics;

namespace NSchema.Model.CompositeTypes;

/// <summary>
/// Represents a database composite type: a schema-scoped named tuple of typed <see cref="Fields"/>.
/// </summary>
/// <param name="name">The name of the composite type.</param>
/// <param name="fields">The ordered fields (attributes) of the type; may be empty.</param>
[DebuggerDisplay("{Name,nq} (composite type, {Fields.Count} fields)")]
public sealed class CompositeType(SqlIdentifier name, IReadOnlyList<CompositeField>? fields = null) : DatabaseObject(name), IEquatable<CompositeType>
{
    /// <inheritdoc/>
    public override ObjectKind Kind => ObjectKind.CompositeType;

    /// <summary>
    /// The fields (attributes) of the type, matched by name; may be empty.
    /// </summary>
    public IReadOnlyList<CompositeField> Fields { get; init; } = fields ?? [];

    /// <summary>
    /// Returns a copy of the composite type with the given fields, outside any tree.
    /// </summary>
    public CompositeType WithFields(IReadOnlyList<CompositeField> fields) => new(Name, fields) { Comment = Comment };

    internal CompositeType Clone() => new(Name, Fields) { Comment = Comment };

    /// <summary>
    /// Structural equality over the declared definition.
    /// </summary>
    public bool Equals(CompositeType? other) =>
        other is not null
        && Name == other.Name
        && Fields.SequenceEqual(other.Fields);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CompositeType other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, Fields.Count);
}
