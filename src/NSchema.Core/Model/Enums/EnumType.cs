using System.Diagnostics;

namespace NSchema.Model.Enums;

/// <summary>
/// Represents an enum type: a named, ordered set of string values.
/// </summary>
/// <param name="name">The name of the enum type.</param>
/// <param name="values">The allowed values, in order.</param>
[DebuggerDisplay("{Name,nq} ({Values.Count} values)")]
public sealed class EnumType(SqlIdentifier name, IReadOnlyList<string>? values = null) : DatabaseObject(name), IEquatable<EnumType>
{
    /// <inheritdoc/>
    public override ObjectKind Kind => ObjectKind.Enum;

    /// <summary>
    /// The allowed values, in order.
    /// </summary>
    public IReadOnlyList<string> Values { get; init; } = values ?? [];

    internal EnumType Clone() => new(Name, Values) { Comment = Comment };

    /// <summary>
    /// Structural equality over the declared definition; the schema and the comment are excluded.
    /// </summary>
    public bool Equals(EnumType? other) =>
        other is not null
        && Name == other.Name
        && Values.SequenceEqual(other.Values, StringComparer.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is EnumType other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, Values.Count);
}
