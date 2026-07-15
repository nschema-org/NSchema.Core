namespace NSchema.Project.Nsql.Syntax.Config;

/// <summary>
/// <c>DATABASE [label] ( key = value, … );</c> — selects and configures the database.
/// </summary>
/// <param name="Label">The plugin the database is reached through (e.g. <c>postgres</c>).</param>
/// <param name="Attributes">The attribute list.</param>
public sealed record DatabaseStatement(Identifier? Label, IReadOnlyList<ConfigAttribute> Attributes) : ConfigStatement(Label, Attributes);
