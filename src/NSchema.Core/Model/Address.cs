namespace NSchema.Model;

/// <summary>
/// The fully-qualified address of something in a database, that points at a node from outside the tree.
/// </summary>
public abstract record Address
{
    /// <summary>
    /// The address as written.
    /// </summary>
    public abstract string Value { get; }

    /// <summary>
    /// The schema this address sits in, or <see langword="null"/> when it names something database-global.
    /// </summary>
    public abstract SqlIdentifier? SchemaName { get; }

    /// <inheritdoc />
    public sealed override string ToString() => Value;
}
