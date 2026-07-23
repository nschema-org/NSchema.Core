using System.Text.Json.Serialization;

namespace NSchema.Model;

/// <summary>
/// Attributes common to the members of a database object (columns, constraints, indexes, triggers).
/// </summary>
/// <remarks>
/// A member's <see cref="Parent"/> is wired by the owning object as the tree is built. Equality between
/// members is structural over the declared definition and deliberately excludes the parent and the comment —
/// the differ compares members from two different trees, and location is identity, not structure.
/// </remarks>
public abstract class DatabaseMember : DatabaseElement
{
    /// <summary>
    /// The member's address, or <see langword="null"/> when it is not yet owned by an object in a tree.
    /// </summary>
    public override MemberAddress? Address =>
        Parent is { Schema: { } schema } ? new MemberAddress(schema.Name, Parent.Name, Name) : null;

    /// <summary>
    /// The object that owns the member, or <see langword="null"/> when it has not been placed in a tree.
    /// </summary>
    [JsonIgnore]
    public DatabaseObject? Parent
    {
        get;
        internal set
        {
            if (Parent is { } parent && value is not null && !ReferenceEquals(parent, value))
            {
                throw new InvalidOperationException(
                    $"{GetType().Name} '{Name}' already belongs to '{parent.Name}' and cannot be attached to " +
                    $"'{value.Name}'; remove it first, or attach a copy instead.");
            }
            field = value;
        }
    }
}
