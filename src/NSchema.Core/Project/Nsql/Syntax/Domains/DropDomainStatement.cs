namespace NSchema.Project.Nsql.Syntax.Domains;

/// <summary>
/// <c>DROP DOMAIN schema.name;</c>
/// </summary>
/// <param name="Name">The dropped domain.</param>
public sealed record DropDomainStatement(QualifiedName Name) : NsqlStatement;
