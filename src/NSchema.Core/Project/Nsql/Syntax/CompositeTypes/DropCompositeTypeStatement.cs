namespace NSchema.Project.Nsql.Syntax.CompositeTypes;

/// <summary>
/// <c>DROP TYPE schema.name;</c>
/// </summary>
/// <param name="Name">The dropped composite type.</param>
public sealed record DropCompositeTypeStatement(QualifiedName Name) : NsqlStatement;
