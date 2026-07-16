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

    /// <inheritdoc />
    public sealed override string ToString() => Value;
}
