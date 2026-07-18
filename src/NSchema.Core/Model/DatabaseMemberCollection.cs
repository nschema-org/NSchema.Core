namespace NSchema.Model;

/// <summary>
/// The members owned by a <see cref="DatabaseObject"/>.
/// </summary>
public sealed class DatabaseMemberCollection<T>()
    : ParentedCollection<DatabaseObject, T>((parent, child) => child.Parent = parent, child => child.Parent = null)
    where T : DatabaseMember;
