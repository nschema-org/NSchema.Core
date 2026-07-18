namespace NSchema.Model;

/// <summary>
/// Attributes common to every database element.
/// </summary>
public abstract class DatabaseElement(SqlIdentifier name)
{
    /// <summary>
    /// The element's name.
    /// </summary>
    public SqlIdentifier Name { get; set; } = name;

    /// <summary>
    /// An optional comment or description for the element.
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// Returns a deep copy of the element, outside any tree.
    /// </summary>
    public abstract DatabaseElement Clone();
}
