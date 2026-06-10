namespace NSchema.Schema.Model;

/// <summary>
/// The identity members every named schema object shares. Implemented by the per-kind model records so the
/// comparer's matching and per-kind diffing skeleton can be written once, generically.
/// </summary>
public interface INamedSchemaObject
{
    /// <summary>
    /// The object's name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The object's previous name, if it has been renamed.
    /// </summary>
    string? OldName { get; }

    /// <summary>
    /// An optional comment or description for the object.
    /// </summary>
    string? Comment { get; }
}
