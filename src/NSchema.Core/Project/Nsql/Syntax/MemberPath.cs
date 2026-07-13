namespace NSchema.Project.Nsql.Syntax;

/// <summary>
/// A change-event target path as written: <c>schema.table.member</c> at the top level, or the
/// unqualified <c>table.member</c> inside a template body (the schema binds at projection).
/// </summary>
/// <param name="Schema">The schema segment, or <see langword="null"/> when written unqualified.</param>
/// <param name="Table">The table segment.</param>
/// <param name="Member">The member (column or constraint) segment.</param>
public sealed record MemberPath(Identifier? Schema, Identifier Table, Identifier Member) : NsqlNode;