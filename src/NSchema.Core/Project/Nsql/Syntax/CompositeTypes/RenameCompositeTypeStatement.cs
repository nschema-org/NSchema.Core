namespace NSchema.Project.Nsql.Syntax.CompositeTypes;

/// <summary>
/// <c>RENAME TYPE schema.name TO name;</c>
/// </summary>
/// <param name="From">The composite type's current address.</param>
/// <param name="To">The name the composite type is renamed to.</param>
public sealed record RenameCompositeTypeStatement(QualifiedName From, Identifier To) : NsqlStatement;
