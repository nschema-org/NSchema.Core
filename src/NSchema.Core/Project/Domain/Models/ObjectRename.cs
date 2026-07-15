using NSchema.Model;
namespace NSchema.Project.Domain.Models;

/// <summary>
/// A schema-level object rename directive.
/// </summary>
/// <remarks>Renames never move an object across containers, so the target is a bare name.</remarks>
/// <param name="From">The object's current address.</param>
/// <param name="To">The declared name the object is renamed to.</param>
public sealed record ObjectRename(ObjectReference From, SqlIdentifier To);
