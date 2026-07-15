namespace NSchema.Project.Nsql.Syntax.Domains;

/// <summary>
/// <c>RENAME DOMAIN schema.name TO name;</c>
/// </summary>
/// <param name="From">The domain's current address.</param>
/// <param name="To">The name the domain is renamed to.</param>
public sealed record RenameDomainStatement(QualifiedName From, Identifier To) : NsqlStatement;
