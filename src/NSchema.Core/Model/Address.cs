using System.Text.Json.Serialization;

namespace NSchema.Model;

/// <summary>
/// The fully-qualified address of something in a database, that points at a node from outside the tree.
/// </summary>
public abstract record Address
{
    /// <summary>
    /// The address as written.
    /// </summary>
    [JsonIgnore]
    public abstract string Value { get; }

    /// <summary>
    /// The schema this address belongs to, or <see langword="null"/> when it names something database-global.
    /// </summary>
    [JsonIgnore]
    public abstract SqlIdentifier? SchemaName { get; }

    /// <summary>
    /// Whether this address covers <paramref name="other"/> — it is <paramref name="other"/> or a container
    /// above it. Coverage runs downward through containment only: a schema covers what it holds, never the
    /// reverse.
    /// </summary>
    /// <param name="other">The address to test for coverage.</param>
    public abstract bool Covers(Address other);

    /// <inheritdoc />
    public sealed override string ToString() => Value;
}
