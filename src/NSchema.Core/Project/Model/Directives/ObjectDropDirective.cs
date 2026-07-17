using NSchema.Model;

namespace NSchema.Project.Model.Directives;

/// <summary>
/// A schema-level object drop directive: the object is explicitly declared dropped.
/// </summary>
/// <param name="Kind">The kind of object being dropped.</param>
/// <param name="Address">The dropped object's address (its current reality).</param>
public sealed record ObjectDropDirective(ObjectKind Kind, ObjectAddress Address);
