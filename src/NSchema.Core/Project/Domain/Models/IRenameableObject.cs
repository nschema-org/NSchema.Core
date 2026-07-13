namespace NSchema.Project.Domain.Models;

/// <summary>
/// A named object that supports rename detection via <c>RENAMED FROM</c>. Implemented by the per-kind model
/// records so the comparer's matching and per-kind diffing skeleton can be written once, generically.
/// </summary>
public interface IRenameableObject : INamedObject
{
    /// <summary>
    /// The object's previous name, if it has been renamed.
    /// </summary>
    SqlIdentifier? OldName { get; }
}
