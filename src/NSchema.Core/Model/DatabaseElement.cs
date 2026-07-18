namespace NSchema.Model;

/// <summary>
/// Attributes common to every database element.
/// </summary>
public abstract class DatabaseElement(SqlIdentifier name)
{
    /// <summary>
    /// The element's name.
    /// </summary>
    public SqlIdentifier Name { get; } = name;

    /// <summary>
    /// An optional comment or description for the element.
    /// </summary>
    public string? Comment { get; init; }
}
