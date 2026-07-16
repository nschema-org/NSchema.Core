namespace NSchema.Model;

/// <summary>
/// The identity members every named, commentable object shares.
/// </summary>
public interface INamedObject
{
    /// <summary>
    /// The object's name.
    /// </summary>
    SqlIdentifier Name { get; }

    /// <summary>
    /// An optional comment or description for the object.
    /// </summary>
    string? Comment { get; }
}
