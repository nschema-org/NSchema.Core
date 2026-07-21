namespace NSchema.Project.Nsql.Syntax.Blocks;

/// <summary>
/// A block: <c>KEYWORD [label] ( key = value, … );</c>. One shape for every keyword; the
/// <paramref name="Keyword"/> says which. The configuration file and the lockfile are both sequences of these.
/// </summary>
/// <param name="Keyword">The keyword the block leads with.</param>
/// <param name="Label">The optional bare label (e.g. the <c>postgres</c> in <c>DATABASE postgres (…)</c>).</param>
/// <param name="Attributes">The attribute list.</param>
public sealed record BlockStatement(BlockKeyword Keyword, Identifier? Label, IReadOnlyList<BlockAttribute> Attributes) : NsqlStatement;
