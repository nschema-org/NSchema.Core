namespace NSchema.Project.Nsql.Syntax.Views;

/// <summary>
/// <c>RENAME VIEW schema.name TO name;</c>
/// </summary>
/// <param name="From">The view's current address.</param>
/// <param name="To">The name the view is renamed to.</param>
public sealed record RenameViewStatement(QualifiedName From, Identifier To) : NsqlStatement;
