using System.Text.Json.Serialization;

namespace NSchema.Model;

/// <summary>
/// The full identity of a schema-level object: what it is and where it lives.
/// </summary>
/// <param name="Kind">The kind of object.</param>
/// <param name="Schema">The schema that contains the object.</param>
/// <param name="Name">The name of the object.</param>
[method: JsonConstructor]
public sealed record ObjectIdentity(ObjectKind Kind, SqlIdentifier Schema, SqlIdentifier Name)
{
    /// <summary>
    /// Creates an identity from a kind and a bare location.
    /// </summary>
    /// <param name="kind">The kind of object being identified.</param>
    /// <param name="address">The object's address.</param>
    public ObjectIdentity(ObjectKind kind, ObjectAddress address) : this(kind, address.Schema, address.Name) { }

    /// <summary>
    /// The identity's location.
    /// </summary>
    public ObjectAddress Address => new(Schema, Name);

    /// <inheritdoc />
    public override string ToString() => $"{Schema}.{Name}";
}
