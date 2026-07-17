using NSchema.Model;

namespace NSchema.Project.Nsql.Syntax;

/// <summary>
/// <c>RENAME &lt;kind&gt; schema.name TO name;</c> for a schema-level object.
/// </summary>
/// <param name="Kind">The kind of object being renamed.</param>
/// <param name="From">The object's current address.</param>
/// <param name="To">The name the object is renamed to.</param>
public sealed record RenameObjectStatement(ObjectKind Kind, QualifiedName From, Identifier To) : NsqlStatement;
