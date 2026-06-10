namespace NSchema.Schema.Model;

/// <summary>
/// The identity members every named, commentable object shares — table members (constraints, indexes) as well
/// as schema-level objects. Backs the comparer's shared table-member diffing skeleton.
/// </summary>
public interface INamedObject
{
    /// <summary>
    /// The object's name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// An optional comment or description for the object.
    /// </summary>
    string? Comment { get; }
}
