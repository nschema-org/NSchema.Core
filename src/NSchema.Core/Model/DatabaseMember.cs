namespace NSchema.Model;

/// <summary>
/// Attributes common to the members of a database object (columns, constraints, indexes, triggers).
/// </summary>
/// <remarks>
/// A member's <see cref="Parent"/> is wired by the owning object as the tree is built. Equality between
/// members is structural over the declared definition and deliberately excludes the parent and the comment —
/// the differ compares members from two different trees, and location is identity, not structure.
/// </remarks>
public abstract class DatabaseMember(SqlIdentifier name) : DatabaseElement(name)
{
    /// <summary>
    /// The object that owns the member, or <see langword="null"/> when it has not been placed in a tree.
    /// </summary>
    public DatabaseObject? Parent
    {
        get;
        internal set
        {
            if (Parent is { } parent && !ReferenceEquals(parent, value))
            {
                throw new InvalidOperationException(
                    $"{GetType().Name} '{Name}' already belongs to '{parent.Name}' and cannot be attached to " +
                    $"'{value?.Name}'; attach a copy instead.");
            }
            field = value;
        }
    }
}
