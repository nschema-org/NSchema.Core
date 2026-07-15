namespace NSchema.Project.Nsql.Syntax.Config;

/// <summary>
/// <c>STATE [label] ( key = value, … );</c> — selects and configures where state is stored.
/// </summary>
/// <param name="Label">The plugin the state is stored through (e.g. <c>file</c>, <c>s3</c>).</param>
/// <param name="Attributes">The attribute list.</param>
public sealed record StateStatement(Identifier? Label, IReadOnlyList<ConfigAttribute> Attributes) : ConfigStatement(Label, Attributes);
