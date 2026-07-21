namespace NSchema.Project.Nsql.Syntax.Config;

/// <summary>
/// A statement of the configuration/lockfile grammar: <c>KEYWORD [label] ( key = value, … );</c>. One shape for
/// every keyword; the <paramref name="Keyword"/> says which. Configuration, lockfile, and project statements
/// never share a file — a file parses as one grammar.
/// </summary>
/// <param name="Keyword">The keyword the statement leads with.</param>
/// <param name="Label">The optional bare label (e.g. the <c>postgres</c> in <c>DATABASE postgres (…)</c>).</param>
/// <param name="Attributes">The attribute list.</param>
public sealed record ConfigStatement(ConfigKeyword Keyword, Identifier? Label, IReadOnlyList<ConfigAttribute> Attributes) : NsqlStatement;
