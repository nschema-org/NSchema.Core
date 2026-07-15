namespace NSchema.Project.Nsql.Syntax.Sequences;

/// <summary>
/// <c>RENAME SEQUENCE schema.name TO name;</c>
/// </summary>
/// <param name="From">The sequence's current address.</param>
/// <param name="To">The name the sequence is renamed to.</param>
public sealed record RenameSequenceStatement(QualifiedName From, Identifier To) : NsqlStatement;
