namespace NSchema.Project.Nsql.Syntax.Sequences;

/// <summary>
/// <c>DROP SEQUENCE schema.name;</c>
/// </summary>
/// <param name="Name">The dropped sequence.</param>
public sealed record DropSequenceStatement(QualifiedName Name) : NsqlStatement;
