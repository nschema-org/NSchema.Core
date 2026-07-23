using System.Text.Json.Serialization;
using NSchema.Model.Schemas;

namespace NSchema.Model;

/// <summary>
/// Attributes common to top-level database objects: the schema-level kinds and the database-global extensions.
/// </summary>
/// <remarks>
/// An object knows what it is (<see cref="Kind"/>) and where it lives (<see cref="Schema"/> — wired by the
/// containing schema as the tree is built), so it owns its own <see cref="Address"/>. Table members carry
/// their owning object the same way (<see cref="DatabaseMember.Parent"/>). Equality between objects is
/// structural over the declared definition and deliberately excludes the parent and the comment — the differ
/// compares objects from two different trees, and location is identity, not structure.
/// </remarks>
public abstract class DatabaseObject : DatabaseElement
{
    /// <summary>
    /// The schema the object belongs to, or <see langword="null"/> when it is database-global or has not been placed in a tree.
    /// </summary>
    [JsonIgnore]
    public Schema? Schema
    {
        get;
        internal set
        {
            if (Schema is { } schema && value is not null && !ReferenceEquals(schema, value))
            {
                throw new InvalidOperationException(
                    $"{Kind} '{Name}' already belongs to schema '{schema.Name}' and cannot be attached " +
                    $"to '{value.Name}'; remove it first, or attach a copy instead.");
            }
            field = value;
        }
    }

    /// <summary>
    /// The kind of object this is.
    /// </summary>
    [JsonIgnore]
    public abstract ObjectKind Kind { get; }

    /// <summary>
    /// The object's address, or <see langword="null"/> when it is database-global or not yet part of a schema
    /// (a global object's address is supplied by the deriving type).
    /// </summary>
    public override Address? Address => Schema is null ? null : new ObjectAddress(Schema.Name, Name, Kind);
}
