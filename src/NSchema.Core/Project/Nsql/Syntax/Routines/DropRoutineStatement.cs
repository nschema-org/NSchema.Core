namespace NSchema.Project.Nsql.Syntax.Routines;

/// <summary>
/// <c>DROP FUNCTION|PROCEDURE|ROUTINE schema.name;</c> — all three spellings record a dropped routine
/// (functions and procedures share one name space; the kind resolves from the current state).
/// </summary>
/// <param name="Name">The dropped routine.</param>
public sealed record DropRoutineStatement(QualifiedName Name) : NsqlStatement;