namespace NSchema.Project.Nsql.Syntax.Routines;

/// <summary>
/// <c>RENAME FUNCTION|PROCEDURE|ROUTINE schema.name TO name;</c>
/// </summary>
/// <param name="From">The routine's current address.</param>
/// <param name="To">The name the routine is renamed to.</param>
public sealed record RenameRoutineStatement(QualifiedName From, Identifier To) : NsqlStatement;
