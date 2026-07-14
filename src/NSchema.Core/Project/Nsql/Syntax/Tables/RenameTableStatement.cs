namespace NSchema.Project.Nsql.Syntax.Tables;

/// <summary>
/// <c>RENAME TABLE schema.name TO name;</c>
/// </summary>
/// <param name="From">The table's current address.</param>
/// <param name="To">The name the table is renamed to.</param>
public sealed record RenameTableStatement(QualifiedName From, Identifier To) : NsqlStatement;
