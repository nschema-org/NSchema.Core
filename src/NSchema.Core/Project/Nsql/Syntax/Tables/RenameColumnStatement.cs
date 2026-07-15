namespace NSchema.Project.Nsql.Syntax.Tables;

/// <summary>
/// <c>RENAME COLUMN schema.table.column TO name;</c>
/// </summary>
/// <param name="From">The column's current address.</param>
/// <param name="To">The name the column is renamed to.</param>
public sealed record RenameColumnStatement(MemberPath From, Identifier To) : NsqlStatement;
