using System.Text.Json.Serialization;

namespace NSchema.Model;

/// <summary>
/// Attributes common to every database element.
/// </summary>
public abstract class DatabaseElement
{
    /// <summary>
    /// The element's name.
    /// </summary>
    public required SqlIdentifier Name { get; set; }

    /// <summary>
    /// An optional comment or description for the element.
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// The element's address, or <see langword="null"/> when it is not yet placed in a tree.
    /// </summary>
    [JsonIgnore]
    public abstract Address? Address { get; }

    /// <summary>
    /// Returns a deep copy of the element, outside any tree.
    /// </summary>
    public abstract DatabaseElement Clone();
}
