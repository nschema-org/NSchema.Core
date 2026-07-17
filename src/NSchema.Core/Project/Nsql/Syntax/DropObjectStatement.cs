using NSchema.Model;

namespace NSchema.Project.Nsql.Syntax;

/// <summary>
/// <c>DROP &lt;kind&gt; schema.name;</c> for a schema-level object.
/// </summary>
/// <param name="Kind">The kind of object being dropped.</param>
/// <param name="Name">The dropped object's address.</param>
public sealed record DropObjectStatement(ObjectKind Kind, QualifiedName Name) : NsqlStatement;
