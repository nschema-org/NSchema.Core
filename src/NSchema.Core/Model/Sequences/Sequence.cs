using System.Diagnostics;

namespace NSchema.Model.Sequences;

/// <summary>
/// Represents a sequence: a named integer generator.
/// </summary>
/// <param name="name">The name of the sequence.</param>
/// <param name="options">The sequence's options; unset options use the provider's defaults.</param>
[DebuggerDisplay("{Name,nq} (sequence)")]
public sealed class Sequence(SqlIdentifier name, SequenceOptions? options = null) : DatabaseObject(name), IEquatable<Sequence>
{
    /// <inheritdoc/>
    public override ObjectKind Kind => ObjectKind.Sequence;

    /// <summary>
    /// The sequence's options; unset options use the provider's defaults.
    /// </summary>
    public SequenceOptions Options { get; init; } = options ?? new SequenceOptions();

    internal Sequence Clone() => new(Name, Options) { Comment = Comment };

    /// <summary>
    /// Structural equality over the declared definition.
    /// </summary>
    public bool Equals(Sequence? other) =>
        other is not null
        && Name == other.Name
        && Options == other.Options;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Sequence other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, Options);
}
