namespace NSchema.Project.Nsql.Syntax.Config;

/// <summary>
/// A statement of the configuration grammar: <c>KEYWORD [label] ( key = value, … );</c>.
/// Configuration and project statements never share a file — a file parses as one grammar or the other.
/// </summary>
/// <param name="Label">The optional bare label (e.g. the <c>postgres</c> in <c>DATABASE postgres (…)</c>).</param>
/// <param name="Attributes">The attribute list.</param>
public abstract record ConfigStatement(Identifier? Label, IReadOnlyList<ConfigAttribute> Attributes) : NsqlStatement;
