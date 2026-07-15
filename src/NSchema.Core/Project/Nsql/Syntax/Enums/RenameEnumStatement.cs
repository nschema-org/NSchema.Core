namespace NSchema.Project.Nsql.Syntax.Enums;

/// <summary>
/// <c>RENAME ENUM schema.name TO name;</c>
/// </summary>
/// <param name="From">The enum type's current address.</param>
/// <param name="To">The name the enum type is renamed to.</param>
public sealed record RenameEnumStatement(QualifiedName From, Identifier To) : NsqlStatement;
