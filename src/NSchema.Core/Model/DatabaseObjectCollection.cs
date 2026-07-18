using NSchema.Model.Schemas;

namespace NSchema.Model;

/// <summary>
/// The schema-level objects owned by a <see cref="Schemas.Schema"/>.
/// </summary>
public sealed class DatabaseObjectCollection<T>()
    : ParentedCollection<Schema, T>((parent, child) => child.Schema = parent, child => child.Schema = null)
    where T : DatabaseObject
{
    internal DatabaseObjectCollection(Schema owner) : this() => Attach(owner);
}

