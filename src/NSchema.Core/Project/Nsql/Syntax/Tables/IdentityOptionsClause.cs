namespace NSchema.Project.Nsql.Syntax.Tables;

/// <summary>
/// The parenthesised options of an <c>IDENTITY</c> clause.
/// </summary>
/// <param name="Start">The <c>START</c> value, or <see langword="null"/>.</param>
/// <param name="Increment">The <c>INCREMENT</c> value, or <see langword="null"/>.</param>
/// <param name="MinValue">The <c>MINVALUE</c> value, or <see langword="null"/>.</param>
public sealed record IdentityOptionsClause(long? Start = null, long? Increment = null, long? MinValue = null) : NsqlNode;