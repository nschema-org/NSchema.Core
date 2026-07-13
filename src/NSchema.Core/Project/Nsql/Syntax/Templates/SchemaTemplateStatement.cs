namespace NSchema.Project.Nsql.Syntax.Templates;

/// <summary>
/// <c>TEMPLATE name [FOR SCHEMA] BEGIN statements… END;</c> — a reusable object group instantiated per
/// applied schema. The body's statements stay unexpanded in the tree; instantiation happens at projection.
/// </summary>
/// <param name="Name">The template name.</param>
/// <param name="Statements">The body statements, unexpanded.</param>
public sealed record SchemaTemplateStatement(Identifier Name, IReadOnlyList<NsqlStatement> Statements) : NsqlStatement;